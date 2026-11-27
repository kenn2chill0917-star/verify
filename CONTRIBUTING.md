# Contributing

Thanks for your interest in improving this project! Contributions of all kinds are welcome —
bug fixes, new game verifiers, documentation, and improvements to the `solanagun-quic` crate.

This repository holds two independent things, and each has its own tooling:

  * **A static HTML game site** — the verifier pages (`index.html`, `dice.html`, `slots.html`,
    …) at the repository root, with shared browser assets under `lib/` and a dev server in
    `serve.mjs`. There is no bundler or build step.
  * **A Rust crate, `solanagun-quic`** — defined by `Cargo.toml`, with sources in `src/` and a
    runnable sample in `examples/send.rs`. See [SOLANAGUN-QUIC.md](SOLANAGUN-QUIC.md).

Only run the checks that cover what you changed — editing a game page does not require `cargo`,
and editing the crate does not require Node.

## Getting started

1. Fork the repository and create a branch for your change.
2. Make your change, following the guidelines below.
3. Run the relevant checks locally (see below).
4. Open a pull request against `main` with a clear description of what and why.

## Working on the static site

Requires Node.js (see the `engines` field in [package.json](package.json): `^22.22.0` or
`>= 24.8.0`). There are no runtime dependencies — the dev server uses only Node builtins.

```sh
npm start        # serve the repository root at http://localhost:8080
PORT=3000 npm start   # listen on a different port
```

Before opening a pull request, install the dev tooling and lint the HTML:

```sh
npm ci
npm run lint:html
```

`npm run lint:html` runs [`html-validate`](https://html-validate.org/) over the root `*.html`
pages using the rules in [.htmlvalidate.json](.htmlvalidate.json). The same command runs in CI,
so please make sure it passes.

Guidelines for game pages:

  * Keep each verifier a **self-contained** HTML file that runs entirely in the browser.
  * Reference shared assets from `lib/` rather than adding new copies or CDN links.
  * Match the structure and style of the existing pages.

## Working on the Rust crate

Requires a stable Rust toolchain. Before opening a pull request, run the same checks CI runs:

```sh
cargo fmt --all -- --check
cargo clippy --all-targets --all-features -- -D warnings
cargo test --all-features
```

You can run the example with:

```sh
cargo run --example send
```

Clippy is run with `-D warnings`, so warnings fail the build — fix them before pushing.

## Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to `main` and on
every pull request. The `rust` job runs `cargo fmt --check`, `cargo clippy -D warnings`, and
`cargo test`; the `web` job runs `npm ci` and `npm run lint:html` on Node 24. Pull requests are
expected to pass CI before they are merged.

## Referring to issues and pull requests

Always use the complete URL form when you reference an issue or a pull request — in commit
messages, pull request descriptions, reviews, and issue comments:

```
see https://github.com/kenn2chill0917-star/verify/issues/26
see https://github.com/kenn2chill0917-star/verify/pull/27
```

Avoid the shorthand forms `#26` and `kenn2chill0917-star/verify#26`. A full URL names exactly
one target no matter where it is read, including in `git log`, email notifications, and mirrors.

## Reporting bugs and security issues

  * For ordinary bugs and feature requests, open an issue:
    https://github.com/kenn2chill0917-star/verify/issues
  * For **security vulnerabilities**, do not open a public issue — follow the private
    disclosure process in [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be dual-licensed under the
[Apache-2.0](LICENSE-APACHE) and [MIT](LICENSE-MIT) licenses, matching the rest of the project.
Unless you explicitly state otherwise, any contribution you intentionally submit for inclusion,
as defined in the Apache-2.0 license, shall be dual licensed as above, without any additional
terms or conditions.
