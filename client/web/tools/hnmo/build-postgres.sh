#!/usr/bin/env bash
# Builds the complete portable PostgreSQL toolset on the target OS and architecture.
set -euo pipefail

output="${1:?Pass an output directory}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
version=16.16
checksum=9d5cd8633043b75740d5d59749c6b7365e6aeb53c47098b1a5e80bb18f87881a
archive="postgresql-$version.tar.bz2"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$work"

curl -fsSL --retry 3 "https://ftp.postgresql.org/pub/source/v$version/$archive" -o "$archive"
printf '%s  %s\n' "$checksum" "$archive" | shasum -a 256 -c -
tar -xjf "$archive"
cd "postgresql-$version"

# The database is loopback-only. Build without optional system libraries that
# would require extra installations on users' computers.
if [ "$(uname -s)" = Darwin ]; then
  sed -i.bak 's|$(libdir)/lib$(NAME)|@rpath/lib$(NAME)|' src/Makefile.shlib
  export LDFLAGS="-Wl,-rpath,@loader_path/../lib -Wl,-rpath,@loader_path/.."
fi
./configure --prefix=/hnmo-postgres --without-icu --without-readline --disable-rpath
make -j "${HNMO_POSTGRES_BUILD_JOBS:-4}"
make install DESTDIR="$work/install"

for part in bin lib share; do
  cp -RL "$work/install/hnmo-postgres/$part" "$output/"
done
# Keep the client tools linked to their bundled libpq after the folder moves.
if [ "$(uname -s)" = Linux ]; then
  for executable in "$output"/bin/* "$output"/lib/lib*.so*; do
    patchelf --set-rpath '$ORIGIN/../lib:$ORIGIN' "$executable"
  done
fi
cp COPYRIGHT "$output/LICENSE.txt"
for tool in initdb pg_ctl postgres pg_dump psql; do
  test -x "$output/bin/$tool"
done
