wget https://nuget.org/nuget.exe
BUILD_DIR=$(dirname -- $0)
mono nuget.exe install NUnit -Version 3.6.0 -OutputDirectory $BUILD_DIR/../deps
mono nuget.exe install NUnit.Runners -Version 3.6.0 -OutputDirectory $BUILD_DIR/../deps
cp $BUILD_DIR/../deps/NUnit.3.6.0/lib/nunit.framework.* $BUILD_DIR/../deps/NUnit
