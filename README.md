SQLPad
======

SQLPad is an experimental SQL editor focused to quick and comfortable work.
The primary reason was to find out why there are not any SQL editors with advanced features
like programming IDEs with their intellisense and semantic analysis. To disprove it's not/hardly possible.

Features
========
SQLPad is mainly inspired by ReSharper plugin to Visual Studio and LINQPad. Provides lightweight editor
but aims at thorough semantic analysis and refactoring features. The main driving factor is to reduce
dummy monkey work to minimum while focusing to results or analysis of a problem.

SQLPad provides:
Smart code completion - schemas, objects, columns, functions, packages, sequences, types, etc.
Semantic analysis - validating references to various database objects, immediately notifying about errors or problems.
Fast overview over database object properties.

SQLPad consists of a generic core application providing the user experience and a vendor specific module implementing
the parser, analyzer, validator and other vendor specific components. As of now the only supported vendor is Oracle (11g, 12c).

Commands
========
CTRL + Minus          - Go to previous edit
CTRL + SHIFT + Minus  - Go to next edit
CTRL + E              - Explain plan
CTRL + Space          - Invoke code completion (depends on caret position)
CTRL + SHIFT + Space  - Show function overloads (only in function parameter list)
CTRL + D              - Duplicate line/selection
CTRL + Shoft + /      - Block comment
CTRL + Alt + /        - Line comment
CTRL + Alt + /        - Line comment
CTRL + Alt + /        - Line comment
CTRL + Alt + /        - Line comment




