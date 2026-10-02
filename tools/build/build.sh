#!/bin/sh
# Сборка ParkApp на Linux — так же, как её соберёт Visual Studio:
# .NET Framework 4.8, C# 7.3, полная компиляция XAML.
#
# Нужно один раз:
#   apt-get install dotnet-sdk-8.0
#   SDK из Ubuntu собран без части для WPF — её берём из NuGet-пакета
#   Microsoft.NET.Sdk.WindowsDesktop 3.0.0 и кладём в Sdks/ (см. install_wpf_sdk ниже).
#
# PresentationBuildTasks 3.0 склеивает пути через «\», как в Windows; на Linux
# это обычный символ имени, поэтому под «склеенные» имена подложены ссылки.
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(cd "$HERE/../.." && pwd)
WORK=${PARKAPP_BUILD_DIR:-/tmp/parkapp-build}
PARENT=$(dirname "$WORK")
NAME=$(basename "$WORK")

install_wpf_sdk() {
    SDK=$(dirname "$(dotnet --list-sdks | tail -1 | sed 's/.*\[\(.*\)\]/\1/')")/$(dotnet --version)
    T="$SDK/Sdks/Microsoft.NET.Sdk.WindowsDesktop"
    [ -f "$T/targets/Microsoft.WinFX.targets" ] && return
    TMP=$(mktemp -d)
    curl -sSL -o "$TMP/p.nupkg" \
      https://api.nuget.org/v3-flatcontainer/microsoft.net.sdk.windowsdesktop/3.0.0/microsoft.net.sdk.windowsdesktop.3.0.0.nupkg
    unzip -q "$TMP/p.nupkg" -d "$TMP/p"
    mkdir -p "$T" && cp -r "$TMP/p/Sdk" "$TMP/p/targets" "$TMP/p/tools" "$T/"
    ln -sf Microsoft.WinFx.targets "$T/targets/Microsoft.WinFX.targets"
}

install_wpf_sdk

rm -rf "$WORK" && mkdir -p "$WORK/src"
cp "$HERE/ParkApp.Build.csproj" "$WORK/"
(cd "$REPO/ParkApp" && tar cf - --exclude=bin --exclude=obj .) | (cd "$WORK/src" && tar xf -)
cd "$WORK" && dotnet restore -v:q >/dev/null
mkdir -p "$WORK/obj/Debug/net48"
rm -rf "$PARENT/$NAME\\src" "$PARENT/$NAME\\obj"
ln -sfn "$WORK/src" "$PARENT/$NAME\\src"
ln -sfn "$WORK/obj" "$PARENT/$NAME\\obj"
ln -sfn "$WORK/src" "$WORK/obj/Debug/net48/\\src"
ln -sfn / "$WORK/obj/Debug/net48/\\"
ln -sfn "$WORK/src" "$WORK/obj/Debug/net48/src"
dotnet build --no-restore -nologo -v:q 2>&1 \
  | grep -E 'error|warning|Build succeeded|Error\(s\)|Warning\(s\)' \
  | sed 's|.*/src/||; s| \[/.*\]||' | sort -u
