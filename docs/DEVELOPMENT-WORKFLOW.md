# Development workflow

The canonical branch is `main`.

## Normal change flow

1. Pull the latest `main`.
2. Create a short-lived feature branch from `main`.
3. Make a focused change with documentation and tests where appropriate.
4. Run the required build, test, and migration checks.
5. Open a pull request targeting `main`.
6. Review and merge the pull request into `main`.
7. Delete the merged feature branch.

In short:

```text
main -> feature branch -> review and tests -> main -> delete feature branch
```

## Initial import

The repository's first substantive project import may be committed directly to `main` when the repository owner explicitly approves it. That exception does not change the normal feature-branch workflow above.

## Required gates

Before an ordinary merge, record the outcome of:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

For persistence changes, also apply the migration to a disposable or local PostgreSQL database and verify rollback where practical.

Do not mark a gate as passed unless the corresponding command was run successfully. Record the database version and distinguish migration generation from a real apply/rollback. The initial import ran no gates; current evidence belongs in [project status](PROJECT-STATUS.md).

For domain/persistence changes, keep the [domain dictionary](DOMAIN-MODEL.md), [data dictionary](DATA-DICTIONARY.md), EF model/migrations and [generated PostgreSQL SQL](sql/DOMAIN-SCHEMA.sql) aligned. Add research references to [source links](SOURCE-LINKS.md), and distinguish implemented capabilities from the [feature backlog](FEATURES.md). Run the SQL verification procedure in [database guidance](DATABASE.md).

For the 5 October 2026 domain review, the repository owner explicitly requested committing and pushing the completed changes to `main`. That approval applies to this delivery; normal future work still follows the feature-branch/PR flow above.
