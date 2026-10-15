# Installing the RAGGit Desktop Client

For an employee PC. Takes about five minutes. The client is a Windows desktop app; all
documents, answers, and models stay on the workstation, so nothing here needs internet access.

> **There is no auto-update.** The app never updates itself. When IT ships a new build, install
> it again (step 2) — your settings in `appsettings.json` are preserved if you keep the file.

---

## What IT gives you

| Item | Example | What it is |
|---|---|---|
| The client package | `RAGGit-Client-win-x64.zip` | the app |
| `appsettings.json` | — | your deployment's workstation URL and key |
| `raggit.crt` | — | the workstation's certificate |
| A Tailscale invite | — | access to the company tailnet |
| A username + password | — | your personal sign-in |

**The client only works on the company tailnet.** The workstation has no public address, so
without Tailscale the app cannot reach it — you will see *"cannot reach AI workstation"*.

---

## 1. Join the company tailnet (Tailscale)

1. Accept the invite IT sent you (or ask IT to invite your email to the tailnet).
2. Install Tailscale: <https://tailscale.com/download/windows>
3. Sign in with the account you were invited with, and confirm the tray icon shows
   **Connected**.

## 2. Unpack the client

1. Extract `RAGGit-Client-win-x64.zip` to a folder you can write to, for example
   `%LOCALAPPDATA%\RAGGit`. **Do not** put it in `C:\Program Files` — the app writes its
   log next to itself and a non-admin process cannot write there.
2. You should have at least these three files together:

   ```
   RAGGit.Client.WPF.exe
   appsettings.json
   raggit.crt
   ```

   `appsettings.json` must sit **next to the .exe**. The app reads it at startup.

## 3. Trust the workstation certificate

The workstation uses HTTPS with a company-issued certificate. Windows will reject it until it
is trusted. This does **not** require administrator rights:

```powershell
certutil -user -addstore Root raggit.crt
```

Expect `Certificate "..." added to store.` and `CertUtil: -addstore command completed
successfully.`

> If you skip this, sign-in fails with a secure-connection error. The app never falls back to
> plain HTTP or bypasses the certificate.

## 4. Check the configuration

Open `appsettings.json` in Notepad. Both values are required:

```json
{
  "Workstation": {
    "Url": "https://<workstation-host>",
    "ApiKey": "<the key IT gave you>",
    "UploadConcurrency": 4
  }
}
```

- `Url` must be the **exact** name IT gave you. If it is a tailnet name, use that name — not a
  raw IP address, or the certificate will not match and sign-in fails.
- `ApiKey` must be present. A URL with no key is treated as an invalid configuration.

## 5. Run it and sign in

1. Double-click `RAGGit.Client.WPF.exe`.
2. Sign in with the **username and password IT created for you**.
3. If sign-in fails, see *Troubleshooting* below.

---

## Using it

- **Dashboard** — library counts at a glance.
- **Ask** — type a question, press Enter. Every answer cites the passages it came from, or says
  exactly *no relevant content found* rather than guessing.
- **Document Library / My Documents** — what is available to search.
- **History** — your past questions and answers.

**Answers can take a while.** The workstation runs the model on its own CPU (or GPU). On a
CPU-only workstation a long answer can take a minute or more; while it works the app shows a live
account of what it is doing — reading your question, searching the library, which sources it found, and
writing the answer — under the message box. This is expected, not a freeze. Do not close the window.

---

## Troubleshooting

| What you see | Cause | Fix |
|---|---|---|
| `cannot reach AI workstation` | not on the tailnet, or the URL is wrong | confirm Tailscale shows Connected; re-check `Url` |
| Secure-connection / certificate error | `raggit.crt` not trusted, or `Url` does not match the certificate | redo step 3; use the exact host name IT gave you |
| `invalid-config` | `appsettings.json` missing, or `ApiKey` empty | confirm the file sits next to the .exe and has both values |
| "Windows protected your PC" (SmartScreen) | the package is not code-signed | **More info** → **Run anyway** |
| Sign-in rejected | wrong password, or the account is deactivated | ask IT to reset it |
| A page does not open, or a button does nothing | a client bug — the app logs it silently | see *Reporting a problem* |

### Reporting a problem

The app writes every unhandled error to:

```
%LOCALAPPDATA%\RAGGit\crash.log
```

If something does not work, send IT the last few entries from that file. It is the fastest way
to diagnose a client problem.

---

## For IT: building the package

On a Windows machine with the .NET 10 SDK:

```powershell
dotnet publish src/RAGGit.Client.WPF -c Release -r win-x64 --self-contained true `
  -p:Platform=x64 -p:PublishSingleFile=true -o ./out/client
```

This produces a build that needs **no .NET runtime** on the employee's PC.

Then, per deployment:

1. Copy `deploy/appsettings.client.template.json` to `out/client/appsettings.json` and set
   `Workstation:Url` (the host name clients use — for a tailnet deployment, the MagicDNS name)
   and `Workstation:ApiKey` (the employee key).
2. Copy the workstation's `raggit.crt` (written by `deploy/bootstrap.sh` to
   `${RAGGIT_DATA_DIR}/certs/raggit.crt`) into `out/client/`.
3. Zip `out/client/` and distribute it. **Never commit `appsettings.json`** — it contains the
   API key.

Each employee also needs a tailnet invite and a personal account (created in the client's
**Admin → People** screen, or with the operator CLI on the workstation).

> **Deploying a fix means redistributing the package.** There is no auto-update mechanism. If you
> want one, it has to be added (for example with Velopack) — it is not implemented today.
