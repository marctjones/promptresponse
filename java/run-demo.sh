#!/usr/bin/env sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT/java"
./mvnw -q compile dependency:build-classpath -Dmdep.outputFile=target/classpath.txt
JAVA_BIN="${JAVA_HOME:+$JAVA_HOME/bin/}java"
if [ -z "${JAVA_HOME:-}" ] && [ -x /opt/homebrew/opt/openjdk/bin/java ]; then JAVA_BIN=/opt/homebrew/opt/openjdk/bin/java; fi
exec "$JAVA_BIN" --add-modules jdk.httpserver -cp "target/classes:$(cat target/classpath.txt)" \
  org.promptresponse.demo.AprDemoServer "$@"
