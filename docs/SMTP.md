# Email (SMTP) configuration

To enable real email delivery (forgot password, notifications), configure your SMTP settings in `appsettings.json` under the `Smtp` section. Example for Gmail (use an App Password):

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": "587",
  "Username": "miitrayana@gmail.com",
  "Password": "fqfnndzrxzxloakq",
  "From": "no-reply@mitrayana.local",
  "EnableSsl": "true"
}
```

After updating the settings, restart the application. If the SMTP host is missing or uses a placeholder (e.g., `smtp.example.com`), the app will surface an error when attempting to send mail so you can fix the configuration quickly.

Notes:
- For Gmail, create an App Password (requires 2FA) and use that in `Password`.
- Check the app logs for errors from `AuthController` if sending fails.
- Ensure `From` is a valid sender address permitted by your SMTP provider.