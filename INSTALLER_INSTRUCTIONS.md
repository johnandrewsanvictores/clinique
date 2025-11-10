# Creating an Installer for Queue System

## Option 1: Using Inno Setup (Recommended - Free & Easy)

### Step 1: Download and Install Inno Setup
1. Go to https://jrsoftware.org/isdl.php
2. Download "Inno Setup 6.x.x" (latest version)
3. Install Inno Setup on your computer

### Step 2: Build the Installer
1. Open `installer-setup.iss` in Inno Setup Compiler
2. Click **Build** → **Compile** (or press F9)
3. The installer will be created in the `installer-output` folder
4. The installer file will be named: `QueueSystemSetup.exe`

### Step 3: Distribute
- Share the `QueueSystemSetup.exe` file
- Users can run it to install Queue System on their computers
- The installer will:
  - Copy all files to Program Files
  - Create Start Menu shortcuts
  - Optionally create Desktop shortcut
  - Handle uninstallation

### Customization
Edit `installer-setup.iss` to customize:
- Company name (line 6)
- Version number (line 5)
- Installation options
- Icons and branding

---

## Option 2: Create a Simple ZIP Package

### Quick Distribution Method
1. Navigate to: `bin\Release\net8.0-windows\win-x64\publish\`
2. Select all files and folders
3. Right-click → Send to → Compressed (zipped) folder
4. Name it: `QueueSystem-v1.0.zip`
5. Share this ZIP file

**Users need to:**
- Extract the ZIP to a folder
- Run `Queue System.exe`
- (Optional) Create their own shortcut

---

## Option 3: Advanced Installer (Commercial)

For more advanced features like:
- Custom branding
- License management
- Update mechanisms
- Database configuration wizards

Consider:
- **Advanced Installer** (has free edition)
- **InstallShield** (commercial)
- **WiX Toolset** (free but complex)

---

## Current Build Details

**Application Name:** Queue System
**Build Location:** `bin\Release\net8.0-windows\win-x64\publish\`
**Executable:** Queue System.exe
**Size:** ~200-250 MB (includes .NET runtime)
**Requirements:** Windows 10/11 64-bit

---

## Testing Your Installer

Before distributing:
1. Test on a clean Windows PC (or VM)
2. Verify all images and sounds load correctly
3. Check database connection settings
4. Test the uninstall process

---

## Need Help?

- Inno Setup Documentation: https://jrsoftware.org/ishelp/
- Inno Setup Examples: Check the "Examples" folder in Inno Setup installation
