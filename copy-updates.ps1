# ============================================
# Stripe micropayment contact form
# Run in PowerShell from any directory
# ============================================

$src = "\\192.168.69.75\seonyx-holding"
$dst = "C:\Users\Steve\Dropbox\VITALIS\Seonyx\NEW_SITE_FEB26"
$log = "$env:USERPROFILE\Desktop\copy-updates-log.txt"

Start-Transcript -Path $log -Force | Out-Null
Write-Host "Log: $log"

try {

    Write-Host "Stripe micropayment contact form" -ForegroundColor Cyan
    Write-Host "From: $src" -ForegroundColor Gray
    Write-Host "To:   $dst" -ForegroundColor Gray
    Write-Host ""

    if (-not (Test-Path $src)) {
        throw "ERROR: Cannot reach share at $src"
    }
    if (-not (Test-Path $dst)) {
        throw "ERROR: Destination not found at $dst"
    }

    Write-Host "Copying new files..." -ForegroundColor Yellow
    $newFiles = @(
        "Database\migrations\add-paid-contact-submissions.sql",
        "Models\PaidContactSubmission.cs",
        "Models\ViewModels\PaidContactViewModel.cs",
        "Controllers\StripeWebhookController.cs",
        "Controllers\LegalController.cs",
        "Views\Contact\Cancel.cshtml",
        "Views\Legal\PrivacyPolicy.cshtml",
        "Views\Legal\TermsAndConditions.cshtml",
        "Views\Legal\Cookies.cshtml"
    )
    foreach ($f in $newFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  NEW: $f" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Updating modified files..." -ForegroundColor Yellow
    $modifiedFiles = @(
        "Models\SeonyxContext.cs",
        "Controllers\ContactController.cs",
        "App_Start\RouteConfig.cs",
        "Views\Contact\Index.cshtml",
        "Views\Contact\Success.cshtml",
        "Views\Shared\_Layout.cshtml",
        "Content\css\site.css",
        "Web.config.template"
    )
    foreach ($f in $modifiedFiles) {
        $destDir = Split-Path (Join-Path $dst $f) -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item (Join-Path $src $f) (Join-Path $dst $f) -Force
        Write-Host "  UPD: $f" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "IMPORTANT - Manual steps needed:" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Install Stripe.net via NuGet Package Manager in VS 2022:" -ForegroundColor White
    Write-Host "   Tools > NuGet Package Manager > Package Manager Console" -ForegroundColor Gray
    Write-Host "   Install-Package Stripe.net" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Add Stripe keys to Web.config (not Web.config.template):" -ForegroundColor White
    Write-Host "   <add key=""StripeSecretKey"" value=""sk_test_..."" />" -ForegroundColor Gray
    Write-Host "   <add key=""StripeWebhookSecret"" value=""whsec_..."" />" -ForegroundColor Gray
    Write-Host "   <add key=""ContactFeeAmountCents"" value=""200"" />" -ForegroundColor Gray
    Write-Host "   Use TEST keys first. Get them from dashboard.stripe.com" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Run the migration SQL against the Seonyx database:" -ForegroundColor White
    Write-Host "   sqlcmd -S localhost -d Seonyx -i Database\migrations\add-paid-contact-submissions.sql" -ForegroundColor Gray
    Write-Host "   (Or open the file in SSMS and execute it.)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. Build (Ctrl+Shift+B) and run (F5)." -ForegroundColor White
    Write-Host ""
    Write-Host "5. Test the contact form end-to-end with Stripe test card:" -ForegroundColor White
    Write-Host "   Card: 4242 4242 4242 4242  Expiry: any future date  CVC: any 3 digits" -ForegroundColor Gray
    Write-Host ""
    Write-Host "6. To test webhooks locally, install the Stripe CLI and run:" -ForegroundColor White
    Write-Host "   stripe listen --forward-to https://localhost:44327/api/stripe/webhook" -ForegroundColor Gray
    Write-Host "   Copy the whsec_... secret printed by the CLI into Web.config." -ForegroundColor Gray
    Write-Host ""
    Write-Host "7. For PRODUCTION deploy, configure the webhook in Stripe Dashboard:" -ForegroundColor White
    Write-Host "   Endpoint URL: https://seonyx.com/api/stripe/webhook" -ForegroundColor Gray
    Write-Host "   Event to listen for: checkout.session.completed" -ForegroundColor Gray
    Write-Host "   Then add the live whsec_... secret to the live site Web.config." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Copy complete!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "FAILED: $_" -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
}
