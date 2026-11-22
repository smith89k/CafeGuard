# Alarm sound choice — short design (approved 2026-11-22)

**Why:** the owner wants to pick the alarm sound, including a spoken warning, not only the siren.
"Thief" was rejected as too accusatory (staff or a passer-by may set it off), so the words are polite but firm.

**What the owner decided**
- Card (OFF only, under the Win+L switch): `🔊 Alarm sound ( Siren | Voice | Both )`.
- When Voice or Both: a Words choice + `( Man | Woman )`. Words:
  1. "Don't touch my laptop!" 2. "Please put the laptop down."
  3. "This laptop is protected. The owner is coming back." 4. "Please don't touch this laptop."
- Defaults: **Both**, "Don't touch my laptop!", **Man**.
- Repeats non-stop until Windows is unlocked; 100% volume as today. Both = voice → 3 s siren → repeat. Voice = phrase, short pause, repeat.
- Test siren plays the chosen sound. Choice is saved in settings.json.

**Approaches:** (a) Windows' built-in voices via System.Speech, made on the laptop — chosen: offline, no files, any words.
(b) Pre-recorded clips — rejected: needs recordings, bigger package, fixed words.

**My calls (builder):** words shown as radio buttons (four lines, no drop-down: easier to read, fits the existing styles).
Voice audio is made in the background at start and on each change, louder (peak-normalized), at the siren's format
(44.1 kHz 16-bit mono) and joined with it into one looping sound. If no English voice exists, or speech fails, it
falls back to the plain siren — the alarm must never be silent.
