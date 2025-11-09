{
    description = "oidc-reverse-proxy";

    inputs = {};

    outputs = { nixpkgs, ... }: {
        nixosModules = {
            default = { ... }: {
                imports = [ ./service.nix ];
            };
        };
        checks.x86_64-linux.package-build = nixpkgs.legacyPackages.x86_64-linux.callPackage ./package.nix { };
    };
}
