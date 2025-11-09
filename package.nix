{
  pkgs,
  ...
}:
pkgs.buildDotnetModule {
    pname = "Cyberboss.OidcReverseProxy";
    version = "1.0.0";

    meta = with pkgs.lib; {
      description = "Simple OIDC Authenticated Reverse Proxy";
      homepage = "https://github.com/Cyberboss/oidc-reverse-proxy";
      license = licenses.mit;
      platforms = platforms.x86_64;
    };

    src = ./.;

    projectFile = "src/Cyberboss.OidcReverseProxy/Cyberboss.OidcReverseProxy.csproj";
    nugetDeps = ./deps.json;

    dotnet-sdk = pkgs.dotnetCorePackages.sdk_9_0;
    dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;

    executables = [ "Cyberboss.OidcReverseProxy" ]; # This wraps "$out/lib/$pname/foo" to `$out/bin/foo`.
}
