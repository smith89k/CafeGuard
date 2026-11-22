#!/usr/bin/env python3
"""
CafeGuard for macOS — laptop anti-theft alarm for coffee shops.

Click "Turn ON" before you go to the bathroom. Your Mac locks.
If someone unplugs the charger, closes the lid, or plugs headphones into the jack,
a loud siren plays at 100% volume until you come back, unlock, and click "Turn OFF".

No extra installs: uses only built-in macOS tools (pmset, ioreg, osascript, afplay, caffeinate)
and Python's tkinter.

Run:  python3 cafeguard_mac.py
"""

import math
import os
import platform
import struct
import subprocess
import tempfile
import threading
import time
import tkinter as tk
import wave
from tkinter import messagebox

if platform.system() != "Darwin":
    raise SystemExit("This version is for macOS. On Windows, use app\\CafeGuard.exe.")


# ---------------------------------------------------------------- siren sound

def make_siren_wav(path: str, seconds: float = 4.0) -> None:
    """Writes a loud two-part siren (fast warble + hi/lo) as a WAV file."""
    rate = 44100
    p1 = p2 = 0.0
    frames = bytearray()
    for i in range(int(rate * seconds)):
        t = i / rate
        if int(t * 2) % 2 == 0:
            f1 = 2100.0 + 1300.0 * 0.5 * (1.0 + math.sin(2 * math.pi * 6.5 * t))
            f2 = f1 * 0.5
        else:
            hi = math.sin(2 * math.pi * 8.0 * t) >= 0
            f1, f2 = (2850.0, 1425.0) if hi else (1950.0, 975.0)
        p1 += 2 * math.pi * f1 / rate
        p2 += 2 * math.pi * f2 / rate
        s = math.sin(p1)
        raw = 0.70 * s + 0.20 * (1.0 if s >= 0 else -1.0) + 0.25 * math.sin(p2)
        frames += struct.pack("<h", int(max(-0.98, min(0.98, raw)) * 32767))
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(bytes(frames))


# ---------------------------------------------------------------- Mac helpers

def run(cmd: list[str], timeout: float = 3.0) -> str:
    try:
        return subprocess.run(cmd, capture_output=True, text=True, timeout=timeout).stdout
    except Exception:
        return ""


def charger_plugged_in() -> bool:
    out = run(["pmset", "-g", "batt"])
    return "AC Power" in out if out else True  # unknown -> assume plugged in (no false alarm)


def lid_closed() -> bool:
    out = run(["ioreg", "-r", "-k", "AppleClamshellState", "-d", "4"])
    return '"AppleClamshellState" = Yes' in out


def headphones_in_jack() -> bool:
    """True when the default output is the Mac's own headphone jack.
    Bluetooth earbuds (AirPods etc.) are ignored, so they can't cause a false alarm."""
    out = run(["system_profiler", "SPAudioDataType"], timeout=8)
    device = ""
    for line in out.splitlines():
        s = line.strip()
        if s.endswith(":"):
            device = s[:-1]
        elif s == "Default Output Device: Yes":
            return "headphone" in device.lower()
    return False


def force_max_volume() -> None:
    run(["osascript", "-e", "set volume output volume 100 without output muted"], timeout=2)


def lock_screen() -> None:
    # Ctrl+Cmd+Q (needs Accessibility permission for Terminal/Python). If that fails, turn the display off,
    # which locks the Mac when "Require password immediately" is on (the macOS default).
    r = subprocess.run(
        ["osascript", "-e", 'tell application "System Events" to keystroke "q" using {control down, command down}'],
        capture_output=True,
    )
    if r.returncode != 0:
        run(["pmset", "displaysleepnow"])


def sleep_blocked() -> bool:
    return any(l.split()[-1:] == ["1"] for l in run(["pmset", "-g"]).splitlines() if "SleepDisabled" in l)


def set_sleep_blocked(block: bool) -> bool:
    """Stops (or allows) sleep when the lid closes. macOS asks for your password."""
    value = 1 if block else 0
    r = subprocess.run(
        ["osascript", "-e", f'do shell script "pmset -a disablesleep {value}" with administrator privileges'],
        capture_output=True,
    )
    return r.returncode == 0


# ---------------------------------------------------------------- app

GREEN, BLUE, RED, DARK_RED = "#166534", "#1E40AF", "#DC2626", "#7F1D1D"


class CafeGuard:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.state = "OFF"  # OFF | ON | ALARM
        self.siren_path = os.path.join(tempfile.gettempdir(), "cafeguard_siren.wav")
        make_siren_wav(self.siren_path)
        self.siren_on = False
        self.siren_proc: subprocess.Popen | None = None
        self.caffeinate: subprocess.Popen | None = None
        self.alarm_request = False   # set by the watcher thread, handled on the UI thread
        self.flash = False

        root.title("CafeGuard")
        root.geometry("380x230")
        root.resizable(False, False)
        root.protocol("WM_DELETE_WINDOW", self.on_close)

        self.banner = tk.Label(root, font=("Helvetica", 18, "bold"), fg="white", height=2)
        self.banner.pack(fill="x")
        self.hint = tk.Label(root, font=("Helvetica", 11), wraplength=350, justify="center")
        self.hint.pack(pady=8)
        self.button = tk.Button(root, font=("Helvetica", 14, "bold"), height=2, width=26, command=self.toggle)
        self.button.pack(pady=4)

        bottom = tk.Frame(root)
        bottom.pack(pady=6)
        self.block_lid = tk.BooleanVar(value=True)
        tk.Checkbutton(bottom, text="Stay awake when lid closes (asks Mac password)",
                       variable=self.block_lid).pack()
        self.test_btn = tk.Button(bottom, text="Test siren (3 s)", command=self.test_siren)
        self.test_btn.pack(pady=2)

        # If CafeGuard was closed badly last time, sleep may still be blocked. Offer to fix it.
        if sleep_blocked():
            if messagebox.askyesno("CafeGuard", "Your Mac is still set to never sleep (from last time).\n\nPut it back to normal?"):
                set_sleep_blocked(False)

        self.refresh()
        threading.Thread(target=self.watch_fast, daemon=True).start()
        threading.Thread(target=self.watch_audio, daemon=True).start()
        self.ui_tick()

    # ---------- on / off ----------

    def toggle(self) -> None:
        if self.state == "OFF":
            self.turn_on()
        else:
            self.turn_off()

    def turn_on(self) -> None:
        if not charger_plugged_in():
            messagebox.showwarning("CafeGuard", "Plug in your charger first, then turn CafeGuard ON.")
            return
        if self.block_lid.get() and not set_sleep_blocked(True):
            if not messagebox.askyesno("CafeGuard", "Lid protection was not turned on (no password given).\n\n"
                                                    "Closing the lid will put the Mac to sleep and stop the alarm.\n\nTurn ON anyway?"):
                return
        self.stop_siren()
        self.caffeinate = subprocess.Popen(["caffeinate", "-ims"])
        self.alarm_request = False
        self.state = "ON"
        self.refresh()
        self.root.after(400, lock_screen)

    def turn_off(self) -> None:
        self.state = "OFF"
        self.stop_siren()
        if self.caffeinate:
            self.caffeinate.terminate()
            self.caffeinate = None
        if sleep_blocked():
            set_sleep_blocked(False)
        self.root.attributes("-topmost", False)
        self.refresh()

    def trigger_alarm(self) -> None:
        if self.state != "ON":
            return
        self.state = "ALARM"
        self.start_siren()
        self.root.deiconify()
        self.root.attributes("-topmost", True)
        self.refresh()

    # ---------- watching (background threads, never touch the window) ----------

    def watch_fast(self) -> None:
        while True:
            if self.state == "ON" and (not charger_plugged_in() or lid_closed()):
                self.alarm_request = True
            elif self.state == "ALARM":
                force_max_volume()
            time.sleep(0.4)

    def watch_audio(self) -> None:
        # system_profiler is slow (1-3 s), so it gets its own thread.
        while True:
            if self.state == "ON" and headphones_in_jack():
                self.alarm_request = True
            time.sleep(2)

    def ui_tick(self) -> None:
        if self.alarm_request:
            self.alarm_request = False
            self.trigger_alarm()
        if self.state == "ALARM":
            self.flash = not self.flash
            color = RED if self.flash else DARK_RED
            self.banner.configure(bg=color)
        self.root.after(250, self.ui_tick)

    # ---------- siren ----------

    def start_siren(self) -> None:
        if self.siren_on:
            return
        self.siren_on = True
        force_max_volume()

        def loop() -> None:
            while self.siren_on:
                self.siren_proc = subprocess.Popen(["afplay", self.siren_path])
                self.siren_proc.wait()

        threading.Thread(target=loop, daemon=True).start()

    def stop_siren(self) -> None:
        self.siren_on = False
        if self.siren_proc and self.siren_proc.poll() is None:
            self.siren_proc.terminate()

    def test_siren(self) -> None:
        if self.state != "OFF":
            return
        self.start_siren()
        self.root.after(3000, lambda: self.state == "OFF" and self.stop_siren())

    # ---------- look ----------

    def refresh(self) -> None:
        if self.state == "OFF":
            self.banner.configure(text="CafeGuard is OFF", bg=GREEN)
            self.hint.configure(text="Plug in your charger, then click Turn ON before you leave.")
            self.button.configure(text="🔒 Turn ON (going to bathroom)")
            self.test_btn.configure(state="normal")
        elif self.state == "ON":
            self.banner.configure(text="🛡️ CafeGuard is ON", bg=BLUE)
            self.hint.configure(text="Guarding. Unplugging the charger, closing the lid or using the headphone jack sounds the alarm.")
            self.button.configure(text="🔓 Turn OFF (I'm back)")
            self.test_btn.configure(state="disabled")
        else:
            self.banner.configure(text="🚨 ALARM 🚨", bg=RED)
            self.hint.configure(text="Click Turn OFF to stop the siren.")
            self.button.configure(text="🔓 Turn OFF (I'm back)")

    def on_close(self) -> None:
        if self.state != "OFF":
            return  # can't close while ON — turn it off first
        self.turn_off()
        self.root.destroy()


if __name__ == "__main__":
    window = tk.Tk()
    CafeGuard(window)
    window.mainloop()
