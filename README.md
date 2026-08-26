# Password Manager

A desktop password manager built with C# and Windows Forms (.NET 8).

Saved entries are encrypted on disk using a key derived from your own master
password, so nothing is stored in plain text.

## Features

**Vault**
- Add, edit, and delete saved entries (service, username, password, URL, notes)
- Entries save automatically after every change - no separate save step
- Search/filter the list by service, username, URL, or notes
- Sort the list by clicking any column header
- Entries that haven't been updated in a while are highlighted, as a nudge to review them

**Passwords**
- Built-in password generator (adjustable length, numbers, symbols)
- Live password strength meter when typing or generating a password
- Warns (without blocking) if a password is reused across entries
- Passwords are masked in the list by default, with a per-entry show/hide toggle
- Copy a password to the clipboard, which clears itself again after certain amount of time

**Usernames**
- Autocomplete suggests usernames/emails you've already used
- A separate, manually editable list of suggestions you can add to yourself,
  independent of what's actually saved

**Account & security**
- Master password login, set up on first run
- Change your master password at any time (safely re-encrypts the vault)
- Back up the encrypted vault to a folder of your choice, and restore from a backup later

## Getting Started

1. Open `MyWindowsFormsApp.sln` in Visual Studio (or VS Code with the C# extension)
2. Build and run the project
3. On first launch, you'll be asked to create a master password
4. Right-click inside the account list to add, edit, or delete entries; use the
   **Settings** menu for master password/backup options

## How it works

- Your master password is never stored - only a secure hash of it (PBKDF2), used
  to check you've entered it correctly
- The same master password is used to derive a separate encryption key for your
  saved entries (its own independent salt - never the same derived value used
  for login), so only you can unlock them
- Entries are encrypted with AES-GCM (authenticated encryption, so a corrupted or
  tampered file fails to open rather than silently returning garbage) and saved to
  `%AppData%\PasswordManager`, outside of this project folder
- Vault writes are atomic (written to a temp file, then swapped in), so an
  interruption like a crash or power loss can't leave the vault half-written
- If the main vault file is ever unreadable, the app automatically falls back to
  the backup copy kept from the previous save

## Built With

- C# / .NET 8
- Windows Forms

## Status

WIP