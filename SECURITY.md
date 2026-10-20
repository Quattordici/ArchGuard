# Security Policy

## Supported versions

Security fixes are applied to the latest released version of Archie. Please update to the latest
release before reporting an issue.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Use GitHub's private vulnerability reporting instead: go to the **Security** tab of this repository and
choose **Report a vulnerability**. This creates a private advisory that only the maintainer can see.

Please include:

- The affected version(s) and the SDK or IDE it ran in
- A description of the issue and its impact
- Steps or a minimal code sample to reproduce it

You can expect an initial response within a few days. Once a fix is available, it will be released to
NuGet and the advisory published, with credit to the reporter if desired.

## Scope

Archie runs inside the compiler and the IDE of every project that installs it, and reads `archie.json` from the
repository. Issues such as crashes or hangs triggered by a crafted `archie.json` or source file, unsafe file access
outside the configured path, supply-chain concerns in the build or release pipeline, or problems in the published
package are in scope.
