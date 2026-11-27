# CLAUDE.md

Guidance for [Claude Code](https://docs.claude.com/en/docs/claude-code) (and other AI agents)
when working in this repository. It mirrors `.github/copilot-instructions.md` — if the two ever
diverge, that file is the source of truth.

## Repository overview

This repository holds two independent things:

- **A static HTML game site.** The playable pages live at the repository root (`index.html`,
  `slots.html`, `crash.html`, `roulette.html`, and so on). Shared assets live in `lib/`
  (Bootstrap, jQuery, React, `tools.js`, `card.js`, `utils.js`, …). There is no bundler or build
  step: the HTML files reference `lib/` directly and are served as-is.
- **A Rust crate, `solanagun-quic`.** A QUIC client for the SolanaGun transaction relay, defined
  by `Cargo.toml` with sources in `src/` and a runnable sample in `examples/send.rs`. Usage is
  documented in `SOLANAGUN-QUIC.md`.

## Building and running

- **Static site:** `npm start` (or `npm run dev`) runs the local dev server in `serve.mjs`, which
  serves the repository root at http://localhost:8080 (override with the `PORT` environment
  variable). `package.json` is ESM (`"type": "module"`) and has no runtime dependencies, so the
  dev server runs without installing anything.
- **Linting the site:** `npm ci` then `npm run lint:html` runs `html-validate` over the root
  `*.html` pages, configured by `.htmlvalidate.json`. `html-validate` is the only devDependency,
  and it is what makes `engines.node` require `^22.22.0 || >= 24.8.0`.
- **Rust crate:** `cargo build` and `cargo test`; run the example with `cargo run --example send`.
  CI also enforces `cargo fmt --check` and `cargo clippy -D warnings`.
- **CI:** `.github/workflows/ci.yml` runs on every push to `main` and on every pull request. The
  `rust` job runs `cargo fmt --check`, `cargo clippy -D warnings`, and `cargo test`; the `web` job
  runs `npm ci` and `npm run lint:html` on Node 24.
- **Deployment:** `.github/workflows/deploy-pages.yml` publishes the repository root to GitHub
  Pages on every push to `main`.

Only run the checks that cover what you changed — editing a game page does not require `cargo`,
and editing the crate does not require the dev server.

## Conventions

- Keep each game verifier a self-contained HTML file that runs entirely in the browser, and
  reference shared assets from `lib/` rather than adding new copies or CDN links.
- Make minimal, focused changes and match the style of the surrounding code.
- For vulnerabilities, follow the private disclosure process in [SECURITY.md](SECURITY.md); for
  general contribution setup see [CONTRIBUTING.md](CONTRIBUTING.md).

## Referring to issues and pull requests

Always use the complete URL form when you reference an issue or a pull request, in commit
messages, pull request descriptions, reviews, and issue comments:

```
see https://github.com/kenn2chill0917-star/verify/issues/26
see https://github.com/kenn2chill0917-star/verify/pull/27
```

Avoid the shorthand forms `#26` and `kenn2chill0917-star/verify#26`. Shorthand depends on GitHub
rendering the text in the right context, and it leaves the target host and repository ambiguous —
a bare `#26` silently resolves against whatever repository the reader is in. A full URL names
exactly one target no matter where it is read, including in `git log`, email notifications, and
mirrors.
