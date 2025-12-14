# DbMetaTool

**DbMetaTool** is a Command Line Interface (CLI) tool for managing Firebird 5.0 database schemas. It allows users to create databases, execute updates, and generate SQL scripts from existing schemas.

## 🚀 Features

* **Build Database (`build-db`)**
    * Creates a pristine, empty Firebird database (`.fdb`) from scratch.
    * Automatically executes SQL DDL scripts to set up the initial structure.
* **Update Database (`update-db`)**
    * **Transactional Safety:** Implements an "all-or-nothing" approach. All commands in a script are executed within a single transaction. If any command fails, the entire update is rolled back to ensure database consistency.
    * **Smart Parsing:** correctly handles Firebird-specific syntax, such as `SET TERM` delimiters for Stored Procedures.
    * **Safety Filters:** Automatically ignores `COMMIT` and `ROLLBACK` commands found in scripts to maintain application-level control over the transaction.
* **Export Schema (`export-scripts`)**
    * Reverse-engineers an existing database.
    * Generates a comprehensive `schema.sql` file containing:
        * **Domains** (including `CHECK` constraints, `DEFAULT` values, and `NOT NULL`).
        * **Tables** (columns, types, nullability, defaults).
        * **Stored Procedures** (source code, input/output parameters).

## 📋 Prerequisites

* **.NET 6.0 SDK** (or newer)
* **Firebird 5.0 Server** running locally or remotely.
* **Firebird Client** (`fbclient.dll`) accessible in the system path or application directory.

## 🛠️ Usage

The tool is executed via the command line. Below are the available commands.

### 1. Build a New Database
Creates a new database file and populates it with structures defined in your SQL scripts.

```bash
DbMetaTool build-db --db-dir <path to db dir> --scripts-dir <path to script dir>
```

### 2. Update Existing Database
Applies changes (migrations) to an existing database safely.

```bash
DbMetaTool update-db --connection-string <connection string> --scripts-dir <path to script dir>
```

### 3. Export Schema (Reverse Engineering)
Generates a schema.sql file from a running database.

```bash
DbMetaTool export-scripts --connection-string <connection string> --output-dir <path to output dir>
```