.\Engine\Binaries\DotNET\GitDependencies.exe --prompt --exclude=CryptoPP/5.6.2/ --exclude=ispc_linux --exclude=ispc_osx --exclude=Mac --exclude=mac --exclude=Darwin --exclude=darwin --exclude=osx --exclude=osx32 --exclude=osx64 --exclude=Linux --exclude=linux --exclude=linux32 --exclude=linux64 --exclude=LinuxAArch64 --exclude=Android --exclude=android --exclude=IOS --exclude=TVOS --exclude=tvos --exclude=HTML5 --exclude=PS4 --exclude=XboxOne --exclude=Switch --exclude=OpenEXR-1.7.1 --exclude=ICU/icu4c-53_1 --exclude=llvm --exclude=cef_binary_3.3071.1611.g4a19305_macosx64 --exclude=cef_binary_3.3071.1611.g4a19305_windows32 --exclude=cef_binary_3.2623.1395.g3034273_linux64/Resources --exclude=cef_binary_3.2623.1395.g3034273_linux64/Release --exclude=EpicOnlineServicesInstaller.exe --exclude=RelWithDebInfo --exclude=Win64/Debug --exclude=Win32 --exclude=win32 --exclude=FBX/2020.2/gcc --exclude=FBX/2020.2/clang --exclude=FBX/2020.2/vs2017/x64/debug --include=OpenSubdiv/3.2.0/lib/Win64/VS2015/RelWithDebInfo
del .\Engine\Source\ThirdParty\FBX\2020.2\lib\clang\* /f /s /q
del .\Engine\Source\ThirdParty\FBX\2020.2\lib\gcc\* /f /s /q 
del .\Engine\Source\ThirdParty\FBX\2020.2\lib\vs2017\x64\debug /f /q
del .\Engine\Source\ThirdParty\ICU\icu4c-64_1\source\test /f /q
del .\Engine\Source\ThirdParty\EOSSDK\SDK\Tools\EpicOnlineServicesInstaller.exe /f /q
del .\Engine\Source\ThirdParty\EOSSDK\SDK\Tools\EOS_DevAuthTool-win32-x64-1.2.1 /f /q
del .\Engine\Source\ThirdParty\EOSSDK\SDK\Tools\EOS_DevAuthTool-win32-x64-1.2.1 /f /q
del .\Engine\Source\ThirdParty\EOSSDK\SDK\Tools\EOS_DevAuthTool-darwin-x64-1.2.1 /f /q
rmdir /s /q ".\.git\ue4-gitdeps" 
rmdir /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\arm64-android
rmdir /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\x64-android
rmdir /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\overlay-x64-uwp
rmdir /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\overlay-arm64-uwp
rmdir /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\x86-android
rmdir  /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\overlay-x64-linux
rmdir  /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\overlay-x64-osx
rmdir  /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\overlay-x64-osx
rmdir /s /q .\Engine\Plugins\Runtime\GeoReferencing\Source\ThirdParty\vcpkg-installed\overlay-arm64-ios
