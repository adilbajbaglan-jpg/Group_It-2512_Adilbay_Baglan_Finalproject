#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
mkdir -p runtimes/linux-x64
gcc -std=c11 -O2 -Wall -Wextra -Werror -fPIC -shared libraryfees.c -o runtimes/linux-x64/liblibraryfees.so
