# Password Manager

A simple desktop password manager built with C# and Windows Forms (.NET 8).

Saved entries are encrypted on disk using a key derived from your own master
password, so nothing is stored in plain text.

## Features

- Master password login, set up on first run
- Change master password at any time
- Add, edit, and delete saved entries (service, username, password, URL, notes)
- Show/hide passwords in the entry list
- Built-in password generator
- Username autocomplete based on previously saved entries
- Entries are encrypted and saved automatically, so nothing is lost when you close the app

## Getting Started

1. Open `MyWindowsFormsApp.sln` in Visual Studio (or VS Code with the C# extension)
2. Build and run the project
3. On first launch, you'll be asked to create a master password
4. Right-click inside the app to add, edit, or delete entries

## How it works

- Your master password is never stored - only a secure hash of it, used to check
  you've entered it correctly
- The same master password is used to derive an encryption key for your saved
  entries, so only you can unlock them
- All entries are encrypted (AES-GCM) and saved to a file in your local
  `%AppData%\PasswordManager` folder

## Built With

- C# / .NET 8
- Windows Forms

## Status

WIP