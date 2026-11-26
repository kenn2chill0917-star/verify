# Security Policy

This project welcomes coordinated, good-faith security research. It covers both halves of the
repository: the static game-verifier site served from the repository root, and the
`solanagun-quic` Rust crate.

## Reporting a vulnerability

Please report suspected vulnerabilities **privately** — do not open a public issue, pull
request, or discussion for a security problem, because that discloses the weakness before a fix
is available.

Use GitHub's private reporting form, which routes the report only to the maintainers:

  * https://github.com/kenn2chill0917-star/verify/security/advisories/new

If private advisories are unavailable to you, open a minimal public issue that says only that you
have a security report and asks for a private contact channel — without any details, proof of
concept, or payloads:

  * https://github.com/kenn2chill0917-star/verify/issues/new

### What to include

A good report lets us reproduce the issue quickly:

  * The affected component — a specific verifier page (for example `dice.html`), the shared
    `lib/` assets, the dev server in `serve.mjs`, or the `solanagun-quic` crate.
  * The version or commit you tested, and the browser, operating system, or toolchain.
  * Clear, step-by-step reproduction instructions and a minimal proof of concept.
  * The impact you believe the issue has, and any suggested remediation.

### What to expect

  * We aim to acknowledge a report within **5 business days**.
  * We will keep you updated on validation, triage, and remediation progress.
  * Once a fix ships, we are happy to credit you in the advisory unless you prefer to remain
    anonymous.

## Scope

In scope:

  * The game-verifier pages (`*.html`) and the vendored browser assets under `lib/`.
  * The static development server `serve.mjs`.
  * The `solanagun-quic` crate under `src/` and `examples/`.

Out of scope:

  * Findings that require a compromised or physically-accessed device, or a modified browser.
  * Reports generated solely by automated scanners with no demonstrated, reproducible impact.
  * Denial of service through sheer traffic volume against the demo site, and best-practice
    suggestions (for example missing hardening headers) with no concrete exploit.
  * Vulnerabilities in third-party dependencies that already have an upstream advisory — report
    those to the upstream project, though we still appreciate a heads-up.

## Safe harbor

We will not pursue or support legal action against researchers who act in good faith, stay
within this policy, test only against assets in scope, avoid privacy violations and service
disruption, and give us a reasonable chance to remediate before any public disclosure.
