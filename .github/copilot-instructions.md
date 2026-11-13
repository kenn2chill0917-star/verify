# Copilot instructions

## Repository overview

This repository holds two independent things:

- **A static HTML game site.** The playable pages live at the repository root
  (`index.html`, `slots.html`, `crash.html`, `roulette.html`, and so on). Shared assets live in
  `lib/` (Bootstrap, jQuery, React, `tools.js`, `card.js`, `utils.js`, …). There is no bundler or
  build step: the HTML files reference `lib/` directly and are served as-is.
- **A Rust crate, `solanagun-quic`.** A QUIC client for the SolanaGun transaction relay, defined by
  `Cargo.toml` with sources in `src/` and a runnable sample in `examples/send.rs`. Usage is
  documented in `SOLANAGUN-QUIC.md`.

## Building and running

- Static site: `npm start` (or `npm run dev`) runs the local dev server in `serve.mjs`, which serves
  the repository root at http://localhost:8080 (override with the `PORT` environment variable).
  `package.json` is ESM (`"type": "module"`) and has no dependencies, so no install is required.
- Rust crate: `cargo build` and `cargo test`; run the example with `cargo run --example send`.
- Deployment: `.github/workflows/deploy-pages.yml` publishes the repository root to GitHub Pages on
  every push to `main`.

Only run the checks that cover what you changed — editing a game page does not require `cargo`, and
editing the crate does not require the dev server.

## Referring to issues and pull requests

Always use the complete URL form when you reference an issue or a pull request, in commit messages,
pull request descriptions, reviews, and issue comments:

```
see https://github.com/jlord/sheetsee.js/issues/26
see https://github.com/jlord/sheetsee.js/pull/27
```

Avoid the shorthand forms `#26` and `jlord/sheetsee.js#26`. Shorthand depends on GitHub rendering
the text in the right context, and it leaves the target host and repository ambiguous — a bare
`#26` silently resolves against whatever repository the reader is in. A full URL names exactly one
target no matter where it is read, including in `git log`, email notifications, and mirrors.

This applies to references to this repository as well: write
`https://github.com/kenn2chill0917-star/verify/issues/26`, not `#26`.
