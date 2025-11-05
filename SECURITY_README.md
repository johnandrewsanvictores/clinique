# Queue System - Security Configuration

## ⚠️ IMPORTANT: Connection String Security

The connection string is now stored in `appsettings.json` which is **gitignored** to prevent accidentally committing sensitive credentials.

## Setup Instructions

### For Development

1. **Copy the example configuration file:**
   ```powershell
   Copy-Item appsettings.example.json appsettings.json
   ```

2. **Edit `appsettings.json` with your actual Azure SQL credentials:**
   - Replace `YOUR_SERVER` with your Azure SQL server name
   - Replace `YOUR_DATABASE` with your database name
   - Replace `YOUR_USERNAME` with your database username
   - Replace `YOUR_PASSWORD` with your actual password

3. **Never commit `appsettings.json` to Git!**
   - This file is already in `.gitignore`
   - Always check before pushing: `git status`

### For New Team Members

1. Ask the team lead for the connection string
2. Create your own `appsettings.json` file locally
3. Never share credentials in chat/email

### For Production Deployment

Consider using:
- **Azure Key Vault** for storing secrets
- **Managed Identity** for authentication
- **Environment Variables** on the server

## Files in This Project

- `appsettings.json` - **DO NOT COMMIT** - Contains your actual connection string
- `appsettings.example.json` - Safe to commit - Template for other developers
- `.gitignore` - Ensures `appsettings.json` is never committed

## 🚨 If You Accidentally Committed Credentials

1. **Change your database password immediately** in Azure Portal
2. Remove the file from Git history:
   ```powershell
   git filter-branch --force --index-filter "git rm --cached --ignore-unmatch appsettings.json" --prune-empty --tag-name-filter cat -- --all
   ```
3. Force push (only if safe for your team):
   ```powershell
   git push origin --force --all
   ```

## Security Best Practices

✅ **DO:**
- Use strong, unique passwords
- Rotate credentials regularly
- Use least-privilege principle for database users
- Enable Azure SQL firewall rules

❌ **DON'T:**
- Commit `appsettings.json` to version control
- Share credentials via email/chat
- Use the same password across environments
- Leave default or weak passwords
