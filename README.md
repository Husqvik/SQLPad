SQLPad
======

SQLPad is an experimental SQL editor focused to quick and comfortable work.
The primary reason for it was to find out why there are not any SQL editors with advanced features
like programming IDEs with their intellisense and semantic analysis. To disprove it's not/hardly possible.

Features
--------
SQLPad is mainly inspired by ReSharper plugin to Visual Studio and LINQPad. Provides lightweight editor
but aims at thorough semantic analysis and refactoring features. The main driving factor is to reduce
dummy monkey work to minimum while focusing to results or analysis of a problem.

SQLPad provides:
* Smart code completion - schemas, objects, columns, functions, packages, sequences, types, etc.
* Semantic analysis - validating references to various database objects, immediately notifying about errors or problems.
* Fast overview over database object properties.

SQLPad consists of a generic core application providing the user experience and a vendor specific module implementing
the parser, analyzer, validator and other vendor specific components. As of now the only supported vendor is Oracle (11g, 12c).

Since the implementation is still very experimental there are tons of missing features. Primary focus of implementation are queries, then DML. But there is some DDL supported at least by grammar.

Commands
--------
CTRL + Minus               - Go to previous edit

CTRL + SHIFT + Minus       - Go to next edit

CTRL + E                   - Explain plan

CTRL + Space               - Invoke code completion (depends on caret position)

CTRL + SHIFT + Space       - Show function overloads (only in function parameter list)

CTRL + D                   - Duplicate line/selection

CTRL + SHIFT + /           - Block comment

CTRL + ALT + /             - Line comment

ALT + Enter                - Context actions (depends on caret position)

CTRL + ALT + PageUp        - Navigate to previous highlight

CTRL + ALT + PageDown      - Navigate to next highlight

CTRL + ALT + Home          - Navigate to query block root (related SELECT keyword)

F12				           - Navigate to definition, e. g., when column is propagated via multiple inline views

CTRL + Enter               - Execute command (hold SHIFT to gather execution statistics)

F9                         - Execute command (hold SHIFT to gather execution statistics)

CTRL + D                   - Duplicate line/selection

CTRL + S                   - Save current document

CTRL + SHIFT + S           - Save all open documents

CTRL + Comma               - Show recent documents

CTRL + ALT + F             - Format statement

SHIFT + ALT + F            - Format statement into single line

SHIFT + ALT + F11          - Find usages (column/object identifiers/aliases, functions, bind variables, literals)

ESC                        - Cancel executing command

F5                         - Refresh database metadata

CTRL + T                   - Creates new document

CTRL + F4                  - Closes current document

CTRL + ALT + SHIFT + Up    - Move content up - select column, group by/order by expression

CTRL + ALT + SHIFT + Down  - Move content down - select column, group by/order by expression

ALT + Delete               - Safe delete (column or object alias)

CTRL + SHIFT + U           - Set text to upper case

CTRL + SHIFT + L           - Set text to lower case

Context actions
---------------
@SELECT:

Wrap current query block as inline view

Wrap current query block as common table expression

Unquote - makes all query block columns case insensitive

Unnest inline view

Toggle quoted notation

Toggle fully qualified references

Clean redundant symbols

Add CREATE TABLE AS


@Identifier:

Clean redundant symbols

Resolve ambiguous column

Propagate column

Create script

Add missing column

Add to GROUP BY


@Row source:

Add alias

Create script


@Bind variable:

Convert to literal


@Literal

Convert to bind variable


@*

Expand asterisk (hold SHIFT to select specific column)


@INTO

Add columns (hold SHIFT to select specific column)
