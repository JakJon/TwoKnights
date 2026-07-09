#!/bin/bash
# Minimal MCP-for-Unity JSON-RPC client (streamable HTTP + SSE)
URL="http://127.0.0.1:8080/mcp"
init() {
  curl -s -D /tmp/mcp_headers.txt -o /tmp/mcp_init.txt -X POST "$URL" \
    -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude-curl","version":"1.0"}}}'
  SID=$(grep -i "^mcp-session-id:" /tmp/mcp_headers.txt | tr -d '\r' | awk '{print $2}')
  curl -s -X POST "$URL" -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
    -H "mcp-session-id: $SID" \
    -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' > /dev/null
  echo "$SID"
}
call() {
  SID="$1"; METHOD="$2"; PARAMS="$3"
  curl -s -X POST "$URL" -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
    -H "mcp-session-id: $SID" \
    -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"$METHOD\",\"params\":$PARAMS}" | grep "^data: " | tail -1 | sed 's/^data: //'
}
"$@"
