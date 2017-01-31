#!/bin/bash

LUAJIT_VER="lua-5.1.5"

# Build liblua.a for x86_64
make -C $LUAJIT_VER clean
make macosx -j2 -C $LUAJIT_VER BUILDMODE=static
cp $LUAJIT_VER/src/liblua.a osx/liblua-x86_64.a
# Build wutlua.bundle
cd osx/
xcodebuild
cd ..
# Copy to target folder
mv -r osx/Build/Release/wutlua.bundle ../Plugins/

make -C $LUAJIT_VER clean
/Users/jo3l/Projects/wutLua/Source/osx/wutlua/wutlua-Info.plist
echo "==== Successfully built Plugins/wutlua.bundle ===="
