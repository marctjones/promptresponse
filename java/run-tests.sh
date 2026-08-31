#!/usr/bin/env sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT/java"
./mvnw -q test-compile dependency:build-classpath -Dmdep.outputFile=target/classpath.txt -Dmdep.includeScope=test
JAVA_BIN="${JAVA_HOME:+$JAVA_HOME/bin/}java"
if [ -z "${JAVA_HOME:-}" ] && [ -x /opt/homebrew/opt/openjdk/bin/java ]; then JAVA_BIN=/opt/homebrew/opt/openjdk/bin/java; fi
exec "$JAVA_BIN" -ea -cp "target/test-classes:target/classes:$(cat target/classpath.txt)" \
  org.promptresponse.AprConformanceTest "$ROOT/tests/Conformance/beta6"
