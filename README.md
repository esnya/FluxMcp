ProtoFlux MCP Server Mod
========================

This project provides a simple MonkeyLoader mod implementing a prototype MCP server.
It exposes placeholders for controlling ProtoFlux nodes in Resonite. Current features
include basic TCP handling and an in-memory node store. Full interaction with
Resonite will require further implementation.

Text Protocol
-------------

The server uses a simple line based protocol over TCP. Each command is a single
line with space separated arguments. A single line response is written for every
command. Unknown commands return `error`.

Supported commands:

```
CREATE <name> <type>
FIND <name>
GET_OUTPUT_DISPLAY <id>
GET_INPUT_FIELDS <id>
GET_OUTPUT_FIELDS <id>
CONNECT_INPUT <id> <field> <targetId>
CONNECT_OUTPUT <id> <field> <targetId>
GET_CONNECTION <id> <field>
CALL <id>
IMPULSE <id>
```

Example
-------

```
CREATE Counter basic
# -> 123e4567-e89b-12d3-a456-426614174000
FIND Counter
# -> 123e4567-e89b-12d3-a456-426614174000:Counter
GET_OUTPUT_DISPLAY 123e4567-e89b-12d3-a456-426614174000
# ->
CALL 123e4567-e89b-12d3-a456-426614174000
# -> ok
```

