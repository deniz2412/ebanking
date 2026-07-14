# Use cases

Source: thesis §4.1.1, Table 4-1.

The prototype covers the everyday actions of a retail banking customer. Together they form
a full loop from credential entry through asynchronous transaction confirmation — which is
also the substrate for the attack demonstrations.

| UC-ID | Action (BS) | Action (EN) | Primary service(s) |
|-------|-------------|-------------|--------------------|
| UC-01 | Prijava | Login | Identity (**Keycloak**) |
| UC-02 | Pregled stanja | View balance | Account-Service |
| UC-03 | Historija transakcija | Transaction history | Account-Service, Audit-Service |
| UC-04 | Interni transfer | Internal transfer | Transfer-Service |
| UC-05 | Eksterni transfer | External transfer | Transfer-Service, Payment-Service |
| UC-06 | Plaćanje računa | Bill payment | Payment-Service |
| UC-07 | Trajni nalog | Standing order | Transfer-Service |
| UC-08 | Upravljanje uzorcima | Manage templates/payees | Payment-Service |
| UC-09 | Preuzimanje PDF izvoda | Download PDF statement | Account-Service |
| UC-10 | Promjena lozinke | Change password | Identity (**Keycloak**) |
| UC-11 | Odjava | Logout | Identity (**Keycloak**) |
| UC-12 | Obavještenja | Notifications | Notification-Service |

> Individual per-action activity diagrams are noted in the thesis as living in the appendix.

## Golden path (the flow to build first)

Login (UC-01) → View balance (UC-02) → Internal transfer (UC-04) → Notification (UC-12) →
Logout (UC-11).

- After MFA login, the SPA receives a **short-lived, narrowly-scoped JWT** (least
  privilege).
- The SPA immediately fetches the account list + balances and renders balance and a
  spending chart on the home screen.
- On transfer, the service checks limits and available balance; on successful booking it
  emits a Kafka event.
- Bill payment (UC-06) stays in a **`pending`** state until the user confirms.
- Logout invalidates the token and closes the session to prevent **replay** attacks.

This golden path exercises all three trust boundaries and is the minimum needed before the
vulnerable-pass attack demos in doc 08.
