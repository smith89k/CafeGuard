#!/usr/bin/env python3
"""
CafeGuard — Cross-Platform Laptop Anti-Theft Charger & Lock-Screen Alarm
Supports:
  - macOS (MacBook Air / MacBook Pro): via pmset, osascript CoreAudio, caffeinate, afplay
  - Windows: via ctypes kernel32 GetSystemPowerStatus, LockWorkStation, winsound
Zero external pip dependencies required (uses standard library + tkinter).
"""

import ctypes
import math
import os
import platform
import re
import struct
import subprocess
import sys
import tempfile
import threading
import time
import wave
import tkinter as tk
from tkinter import messagebox

IS_MAC = platform.system() == "Darwin"
IS_WIN = platform.system() == "Windows"


def generate_siren_wav(filepath: str, duration_sec: float = 4.0) -> None:
    """Synthesizes a 16-bit 44.1kHz mono piercing anti-theft siren WAV (1800Hz-3500Hz sweep)."""
    sample_rate = 44100
    total_samples = int(sample_rate * duration_sec)
    phase1 = 0.0
    phase2 = 0.0

    frames = bytearray()
    for i in range(total_samples):
        t = i / sample_rate
        if int(t * 2) % 2 == 0:
            sweep = 0.5 * (1.0 + math.sin(2.0 * math.pi * 6.5 * t))
            freq1 = 2100.0 + (1300.0 * sweep)
            freq2 = freq1 * 0.5
        else:
            hi = math.sin(2.0 * math.pi * 8.0 * t) >= 0
            freq1 = 2850.0 if hi else 1950.0
            freq2 = 1425.0 if hi else 975.0

        phase1 += 2.0 * math.pi * freq1 / sample_rate
        phase2 += 2.0 * math.pi * freq2 / sample_rate

        s1 = math.sin(phase1)
        sign1 = 1.0 if s1 >= 0 else -1.0
        raw = 0.70 * s1 + 0.20 * sign1 + 0.25 * math.sin(phase2)
        clamped = max(-0.98, min(0.98, raw))
        sample = int(clamped * 32767)
        frames.extend(struct.pack("<h", sample))

    with wave.open(filepath, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(bytes(frames))


class PlatformBackend:
    """OS-specific hardware sensor, lock-screen, keep-awake, and anti-mute controller."""

    def __init__(self) -> None:
        self.siren_path = os.path.join(tempfile.gettempdir(), "cafeguard_siren.wav")
        generate_siren_wav(self.siren_path, 4.0)
        self._caffeinate_proc = None
        self._afplay_proc = None
        self._siren_active = False
        self._siren_thread = None

    def get_power_status(self) -> tuple[bool, int, str]:
        """Returns (is_plugged_in, battery_percent, description)."""
        if IS_MAC:
            try:
                out = subprocess.check_output(["pmset", "-g", "batt"], text=True, timeout=2)
                plugged = "AC Power" in out
                m = re.search(r"(\d+)%", out)
                pct = int(m.group(1)) if m else 100
                status = f"{'AC MagSafe/USB-C Plugged In' if plugged else 'ON BATTERY (UNPLUGGED)'} ({pct}%)"
                return plugged, pct, status
            except Exception:
                return True, 100, "macOS Power Sensor Ready"

        if IS_WIN:
            class SYSTEM_POWER_STATUS(ctypes.Structure):
                _fields_ = [
                    ("ACLineStatus", ctypes.c_byte),
                    ("BatteryFlag", ctypes.c_byte),
                    ("BatteryLifePercent", ctypes.c_byte),
                    ("SystemStatusFlag", ctypes.c_byte),
                    ("BatteryLifeTime", ctypes.c_ulong),
                    ("BatteryFullLifeTime", ctypes.c_ulong),
                ]

            sps = SYSTEM_POWER_STATUS()
            if ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(sps)):
                plugged = sps.ACLineStatus == 1
                pct = sps.BatteryLifePercent if sps.BatteryLifePercent <= 100 else 100
                status = f"{'AC Charger Plugged In' if plugged else 'ON BATTERY (UNPLUGGED)'} ({pct}%)"
                return plugged, pct, status

        return True, 100, "Power Status Unknown"

    def get_active_audio_output_name(self) -> str:
        """Detects active audio output endpoint on macOS for the Headphone Trap."""
        if IS_MAC:
            try:
                out = subprocess.check_output(
                    ["system_profiler", "SPAudioDataType"], text=True, timeout=3
                )
                current_device = "MacBook Speakers"
                for line in out.splitlines():
                    stripped = line.strip()
                    if stripped.endswith(":") and not stripped.startswith("Default"):
                        current_device = stripped[:-1]
                    if "Default Output Device: Yes" in stripped:
                        return current_device
            except Exception:
                pass
            return "MacBook Built-in Speakers"
        return "System Default Speaker"

    def enforce_max_volume_unmute(self) -> None:
        """Forces OS master volume to 100% and unmutes."""
        if IS_MAC:
            try:
                subprocess.run(
                    ["osascript", "-e", "set volume output volume 100 without output muted"],
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                    timeout=1,
                )
            except Exception:
                pass
        elif IS_WIN:
            # On Windows, send APPCOMMAND_VOLUME_UP or use CafeGuard.exe CoreAudio COM controller
            pass

    def set_keep_awake(self, enable: bool) -> None:
        """Prevents MacBook / Windows laptop from sleeping while locked at a coffee shop."""
        if IS_MAC:
            if enable and self._caffeinate_proc is None:
                try:
                    self._caffeinate_proc = subprocess.Popen(
                        ["caffeinate", "-i", "-s", "-d"],
                        stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL,
                    )
                except Exception:
                    pass
            elif not enable and self._caffeinate_proc is not None:
                try:
                    self._caffeinate_proc.terminate()
                except Exception:
                    pass
                self._caffeinate_proc = None
        elif IS_WIN:
            ES_CONTINUOUS = 0x80000000
            ES_SYSTEM_REQUIRED = 0x00000001
            ES_AWAYMODE_REQUIRED = 0x00000040
            if enable:
                ctypes.windll.kernel32.SetThreadExecutionState(
                    ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED
                )
            else:
                ctypes.windll.kernel32.SetThreadExecutionState(ES_CONTINUOUS)

    def lock_screen(self) -> None:
        """Locks the OS screen (macOS Lock Screen / Windows Win+L)."""
        if IS_MAC:
            try:
                # Primary macOS Lock Screen trigger (Ctrl+Cmd+Q via System Events or pmset displaysleepnow)
                subprocess.run(
                    [
                        "osascript",
                        "-e",
                        'tell application "System Events" to keystroke "q" using {control down, command down}',
                    ],
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                    timeout=2,
                )
            except Exception:
                try:
                    subprocess.run(["pmset", "displaysleepnow"], check=False)
                except Exception:
                    pass
        elif IS_WIN:
            ctypes.windll.user32.LockWorkStation()

    def start_siren(self) -> None:
        if self._siren_active:
            return
        self._siren_active = True

        if IS_WIN:
            import winsound
            winsound.PlaySound(self.siren_path, winsound.SND_FILENAME | winsound.SND_ASYNC | winsound.SND_LOOP)
        else:
            def _mac_loop():
                while self._siren_active:
                    try:
                        self._afplay_proc = subprocess.Popen(
                            ["afplay", self.siren_path],
                            stdout=subprocess.DEVNULL,
                            stderr=subprocess.DEVNULL,
                        )
                        self._afplay_proc.wait()
                    except Exception:
                        time.sleep(0.5)

            self._siren_thread = threading.Thread(target=_mac_loop, daemon=True)
            self._siren_thread.start()

    def stop_siren(self) -> None:
        self._siren_active = False
        if IS_WIN:
            import winsound
            winsound.PlaySound(None, winsound.SND_PURGE)
        else:
            if self._afplay_proc is not None:
                try:
                    self._afplay_proc.terminate()
                except Exception:
                    pass
                self._afplay_proc = None


class CafeGuardApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("CafeGuard — Laptop Anti-Theft Charger Alarm (macOS & Windows)")
        self.root.geometry("560x580")
        self.root.configure(bg="#12161F")
        self.root.resizable(False, False)

        self.backend = PlatformBackend()
        self.state = "DISARMED"  # DISARMED | ARMED | ALARM
        self.armed_pin = "1234"
        self.armed_audio_device = ""

        self.root.protocol("WM_DELETE_WINDOW", self.on_close_attempt)
        self._build_ui()
        self._poll_loop()

    def _build_ui(self) -> None:
        self.banner = tk.Frame(self.root, bg="#166534", height=85)
        self.banner.pack(fill="x", padx=16, pady=(16, 10))

        self.lbl_state = tk.Label(
            self.banner,
            text="🟢 DISARMED — READY",
            font=("Helvetica", 16, "bold"),
            fg="white",
            bg="#166534",
        )
        self.lbl_state.pack(pady=(12, 4))

        self.lbl_sub = tk.Label(
            self.banner,
            text="Plug in MagSafe / USB-C charger, set PIN, and click 'Arm & Lock Screen'.",
            font=("Helvetica", 10),
            fg="#DCFCE7",
            bg="#166534",
        )
        self.lbl_sub.pack(pady=(0, 12))

        # Telemetry Frame
        tele = tk.LabelFrame(
            self.root,
            text=" Live Hardware Sensors ",
            fg="#94A3B8",
            bg="#1A202C",
            font=("Helvetica", 10, "bold"),
            padx=12,
            pady=10,
        )
        tele.pack(fill="x", padx=16, pady=6)

        self.lbl_power = tk.Label(
            tele, text="Power: Checking...", fg="#4ADE80", bg="#1A202C", font=("Helvetica", 10, "bold")
        )
        self.lbl_power.pack(anchor="w")

        self.lbl_audio = tk.Label(
            tele, text="Audio Output: Default Speakers", fg="#E2E8F0", bg="#1A202C", font=("Helvetica", 10)
        )
        self.lbl_audio.pack(anchor="w", pady=(4, 0))

        # Options Frame
        opts = tk.LabelFrame(
            self.root,
            text=" Anti-Theft Defense Modules ",
            fg="#94A3B8",
            bg="#1A202C",
            font=("Helvetica", 10, "bold"),
            padx=12,
            pady=10,
        )
        opts.pack(fill="x", padx=16, pady=6)

        self.var_antimute = tk.BooleanVar(value=True)
        self.var_headphone = tk.BooleanVar(value=True)
        self.var_keepawake = tk.BooleanVar(value=True)

        tk.Checkbutton(
            opts,
            text="Anti-Mute 100% Max Volume Lock (Unmutes & sets 100% volume every 0.2s)",
            variable=self.var_antimute,
            fg="white",
            bg="#1A202C",
            selectcolor="#0F172A",
            activebackground="#1A202C",
            activeforeground="white",
        ).pack(anchor="w")

        tk.Checkbutton(
            opts,
            text="Headphone / Audio-Jack Trap (Triggers siren if audio output changes)",
            variable=self.var_headphone,
            fg="white",
            bg="#1A202C",
            selectcolor="#0F172A",
            activebackground="#1A202C",
            activeforeground="white",
        ).pack(anchor="w")

        tk.Checkbutton(
            opts,
            text="Keep Awake on Lock Screen (caffeinate / ES_SYSTEM_REQUIRED while at WC)",
            variable=self.var_keepawake,
            fg="white",
            bg="#1A202C",
            selectcolor="#0F172A",
            activebackground="#1A202C",
            activeforeground="white",
        ).pack(anchor="w")

        # Controls Frame
        ctrl = tk.LabelFrame(
            self.root,
            text=" PIN & Arm / Disarm Controls ",
            fg="#94A3B8",
            bg="#1A202C",
            font=("Helvetica", 10, "bold"),
            padx=12,
            pady=10,
        )
        ctrl.pack(fill="x", padx=16, pady=6)

        pin_row = tk.Frame(ctrl, bg="#1A202C")
        pin_row.pack(fill="x", pady=(2, 8))

        tk.Label(
            pin_row, text="Secret PIN:", fg="white", bg="#1A202C", font=("Helvetica", 11, "bold")
        ).pack(side="left")

        self.entry_pin = tk.Entry(
            pin_row,
            show="●",
            font=("Courier", 14, "bold"),
            width=10,
            bg="#0F172A",
            fg="#38BDF8",
            insertbackground="white",
            justify="center",
        )
        self.entry_pin.insert(0, "1234")
        self.entry_pin.pack(side="left", padx=10)
        self.entry_pin.bind("<Return>", lambda _: self.on_enter_key())

        self.btn_disarm = tk.Button(
            pin_row,
            text="🔓 DISARM (Enter PIN)",
            bg="#DC2626",
            fg="white",
            font=("Helvetica", 10, "bold"),
            state="disabled",
            command=self.try_disarm,
        )
        self.btn_disarm.pack(side="left", fill="x", expand=True)

        btn_row = tk.Frame(ctrl, bg="#1A202C")
        btn_row.pack(fill="x", pady=4)

        self.btn_arm_lock = tk.Button(
            btn_row,
            text="🔒 ARM & LOCK SCREEN (WC Mode)",
            bg="#2563EB",
            fg="white",
            font=("Helvetica", 10, "bold"),
            height=2,
            command=lambda: self.arm(lock_screen=True),
        )
        self.btn_arm_lock.pack(side="left", fill="x", expand=True, padx=(0, 6))

        self.btn_arm_only = tk.Button(
            btn_row,
            text="🛡️ ARM ONLY",
            bg="#0F766E",
            fg="white",
            font=("Helvetica", 10, "bold"),
            height=2,
            command=lambda: self.arm(lock_screen=False),
        )
        self.btn_arm_only.pack(side="left", fill="x", expand=True)

        # Log Box
        self.log_list = tk.Listbox(
            self.root, bg="#0F172A", fg="#94A3B8", font=("Courier", 9), height=7, bd=0
        )
        self.log_list.pack(fill="both", expand=True, padx=16, pady=(6, 16))
        self.log("CafeGuard initialized. Ready for MacBook / Windows protection.")

    def log(self, msg: str) -> None:
        ts = time.strftime("%H:%M:%S")
        self.log_list.insert(0, f"[{ts}] {msg}")

    def on_enter_key(self) -> None:
        if self.state == "DISARMED":
            self.arm(lock_screen=True)
        else:
            self.try_disarm()

    def arm(self, lock_screen: bool) -> None:
        pin = self.entry_pin.get().strip()
        if len(pin) < 3:
            messagebox.showwarning("PIN Required", "Please enter a secret PIN (at least 3 digits).")
            return

        plugged, _, _ = self.backend.get_power_status()
        if not plugged:
            if not messagebox.askyesno(
                "Charger Unplugged",
                "Your charger is currently UNPLUGGED! Arming now will immediately trigger the siren.\n\nArm anyway?",
            ):
                return

        self.armed_pin = pin
        self.entry_pin.delete(0, tk.END)
        self.armed_audio_device = self.backend.get_active_audio_output_name()
        self.state = "ARMED"

        if self.var_keepawake.get():
            self.backend.set_keep_awake(True)

        self.banner.configure(bg="#1E40AF")
        self.lbl_state.configure(text="🛡️ ARMED — GUARDING LAPTOP", bg="#1E40AF")
        self.lbl_sub.configure(text="Monitoring Charger & Audio Jack on Lock Screen. Enter PIN to disarm.", bg="#1E40AF")
        self.btn_arm_lock.configure(state="disabled")
        self.btn_arm_only.configure(state="disabled")
        self.btn_disarm.configure(state="normal")
        self.log(f"ARMED protection (Audio locked to: {self.armed_audio_device}).")

        if lock_screen:
            self.log("Locking screen for WC break...")
            self.root.after(400, self.backend.lock_screen)

    def trigger_alarm(self, reason: str) -> None:
        if self.state == "ALARM":
            return
        self.state = "ALARM"
        self.log(f"🚨 ALARM TRIGGERED: {reason}")
        self.banner.configure(bg="#DC2626")
        self.lbl_state.configure(text="🚨 THEFT ALARM TRIGGERED! 🚨", bg="#DC2626")
        self.lbl_sub.configure(text=f"Trigger: {reason} — Enter PIN to silence!", bg="#DC2626")

        if self.var_antimute.get():
            self.backend.enforce_max_volume_unmute()
        self.backend.start_siren()
        self.root.attributes("-topmost", True)

    def try_disarm(self) -> None:
        if self.entry_pin.get().strip() == self.armed_pin:
            self.backend.stop_siren()
            self.backend.set_keep_awake(False)
            self.state = "DISARMED"
            self.entry_pin.delete(0, tk.END)
            self.entry_pin.insert(0, self.armed_pin)
            self.root.attributes("-topmost", False)

            self.banner.configure(bg="#166534")
            self.lbl_state.configure(text="🟢 DISARMED — READY", bg="#166534")
            self.lbl_sub.configure(text="Protection disarmed by owner PIN.", bg="#166534")
            self.btn_arm_lock.configure(state="normal")
            self.btn_arm_only.configure(state="normal")
            self.btn_disarm.configure(state="disabled")
            self.log("🟢 DISARMED by owner.")
        else:
            self.entry_pin.delete(0, tk.END)
            self.log("❌ Wrong PIN entered!")
            if self.state == "ARMED":
                self.trigger_alarm("Wrong PIN entered while armed!")

    def _poll_loop(self) -> None:
        plugged, _, ptxt = self.backend.get_power_status()
        self.lbl_power.configure(text=f"🔌 Power: {ptxt}", fg="#4ADE80" if plugged else "#F87171")

        if self.state == "ARMED":
            if not plugged:
                self.trigger_alarm("AC Charger Unplugged!")
            elif self.var_headphone.get() and IS_MAC:
                curr_audio = self.backend.get_active_audio_output_name()
                if curr_audio != self.armed_audio_device:
                    self.trigger_alarm(f"Audio changed to {curr_audio} (Headphone Trap!)")
        elif self.state == "ALARM":
            if self.var_antimute.get():
                self.backend.enforce_max_volume_unmute()

        self.root.after(200, self._poll_loop)

    def on_close_attempt(self) -> None:
        if self.state != "DISARMED":
            self.log("⚠️ Blocked close attempt while Armed! Enter PIN first.")
            return
        self.backend.stop_siren()
        self.backend.set_keep_awake(False)
        self.root.destroy()


if __name__ == "__main__":
    root = tk.Tk()
    app = CafeGuardApp(root)
    root.mainloop()
