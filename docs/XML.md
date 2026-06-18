# Sending and Receiving XML

The PassMan API speaks **both JSON and XML** on every endpoint. JSON is the default; XML is
selected per-request via standard HTTP content negotiation. You never need a different URL — the
same endpoints serve either format based on two headers:

| Header | Purpose | Values |
|--------|---------|--------|
| `Accept` | Format you want **back** | `application/json` (default) or `application/xml` |
| `Content-Type` | Format of the body you **send** | `application/json` or `application/xml` |

The two are independent: you can POST XML and ask for a JSON response, or vice-versa.

## Receiving XML (responses)

Add `Accept: application/xml`:

```bash
curl http://localhost:5246/api/vaults \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/xml"
```

```xml
<ArrayOfVaultResponse xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" ...>
  <VaultResponse>
    <Id>1</Id>
    <Name>Personal</Name>
    <IsOwner>true</IsOwner>
  </VaultResponse>
</ArrayOfVaultResponse>
```

Notes:
- A collection serializes as `<ArrayOfXxx>` with one `<Xxx>` element per item.
- **Element names are PascalCase** (the C# property names, e.g. `<AccessToken>`), whereas JSON uses
  camelCase (`accessToken`). This is the XmlSerializer convention.
- Without an `Accept` header (or with `*/*`), you get JSON.

## Sending XML (request bodies)

Set `Content-Type: application/xml` and send the element shape matching the request DTO. Example —
log in with an XML body and get an XML response:

```bash
curl -X POST http://localhost:5246/api/auth/login \
  -H "Content-Type: application/xml" \
  -H "Accept: application/xml" \
  --data '<LoginRequest><Email>user@example.com</Email><Password>Passw0rd!</Password></LoginRequest>'
```

Create a credential from XML:

```bash
curl -X POST http://localhost:5246/api/vaults/1/credentials \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/xml" \
  --data '<CreateCredentialRequest>
            <Title>GitHub</Title>
            <Username>octocat</Username>
            <EncryptedPassword>s3cret</EncryptedPassword>
            <Url>https://github.com</Url>
          </CreateCredentialRequest>'
```

The root element name is the request type (`LoginRequest`, `CreateCredentialRequest`, `RegisterRequest`,
`CreateVaultRequest`, `CreateTagRequest`, `CreateInvitationRequest`, …); child elements are the
PascalCase property names.

## Errors in XML

Error bodies content-negotiate the same way — request `Accept: application/xml` and a 4xx/5xx comes
back as XML instead of JSON:

```xml
<ErrorResponse xmlns:xsi="...">
  <Type>https://tools.ietf.org/html/rfc7231#section-6.5.4</Type>
  <Title>Not Found</Title>
  <Status>404</Status>
  <Detail>Vault not found.</Detail>
  <TraceId>00-...-00</TraceId>
</ErrorResponse>
```

Validation (400) errors include the field errors as a `<errors>` list:

```xml
<ErrorResponse xmlns:xsi="...">
  <Type>https://tools.ietf.org/html/rfc7231#section-6.5.1</Type>
  <Title>One or more validation errors occurred.</Title>
  <Status>400</Status>
  <TraceId>00-...</TraceId>
  <errors>
    <error field="Email"><message>The Email field is required.</message></error>
  </errors>
</ErrorResponse>
```

## How it's wired (for maintainers)

- `Program.cs` registers `AddXmlSerializerFormatters()`, enabling the `XmlSerializer` input/output
  formatters alongside the JSON ones.
- Response DTOs are **classes or nominal records with a parameterless constructor** — `XmlSerializer`
  cannot serialize positional records (no parameterless ctor) or anonymous types, and cannot serialize
  interface-typed members (e.g. `IEnumerable<T>`), so list endpoints return concrete `List<T>`.
- Pipeline errors (the global exception handler and the 401/403 auth-pipeline responses) are written
  by `ErrorResponseWriter`, which negotiates XML/JSON from the `Accept` header outside MVC.
- The user-management controller maps to **`/api/user`** (singular — follows `[controller]` from
  `UserController`), not `/api/users`. All other controllers use plurals (`/api/vaults`, `/api/tags`, …).
