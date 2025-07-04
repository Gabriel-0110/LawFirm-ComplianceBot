# Deploy to GitHub Script
# Run this from the root of your project: c:\Coding\Teams Recording

Write-Host "🚀 Preparing Teams Compliance Bot for GitHub deployment..." -ForegroundColor Green

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "❌ Not in a git repository. Initialize first:" -ForegroundColor Red
    Write-Host "git init" -ForegroundColor Yellow
    Write-Host "git remote add origin https://github.com/yourusername/teams-compliance-bot.git" -ForegroundColor Yellow
    exit 1
}

# Add all the new and updated files
Write-Host "📁 Adding files to git..." -ForegroundColor Blue
git add .

# Show status
Write-Host "`n📋 Git status:" -ForegroundColor Blue
git status

# Commit with a descriptive message
$commitMessage = "feat: Add automatic call joining and Graph webhooks

- ✅ Fixed compilation errors and missing service registrations
- ✅ Enhanced NotificationsController for receiving call notifications
- ✅ Added SubscriptionSetupService for automatic Graph subscriptions
- ✅ Added CallJoiningService integration
- ✅ Updated GitHub Actions workflow for teamsbot webapp
- ✅ Bot now automatically joins calls and starts recording

Critical fixes for Teams compliance bot functionality."

Write-Host "`n💾 Committing changes..." -ForegroundColor Blue
git commit -m $commitMessage

# Push to main branch
Write-Host "`n🚀 Pushing to GitHub..." -ForegroundColor Blue
git push origin main

Write-Host "`n✅ Deployment initiated! Check GitHub Actions for build status." -ForegroundColor Green
Write-Host "🌐 Your bot will be deployed to: https://arandiateamsbot.ggunifiedtech.com" -ForegroundColor Green
Write-Host "`n🔧 Next steps:" -ForegroundColor Yellow
Write-Host "1. Configure GitHub Secrets (see GITHUB_SECRETS_SETUP.md)" -ForegroundColor White
Write-Host "2. Monitor the GitHub Actions workflow" -ForegroundColor White
Write-Host "3. Test the webhook endpoint: https://arandiateamsbot.ggunifiedtech.com/api/graphwebhook" -ForegroundColor White
