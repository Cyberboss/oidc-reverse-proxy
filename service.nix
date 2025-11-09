inputs@{
  config,
  lib,
  pkgs,
  ...
}:

let
  enabled-instances = lib.filterAttrs (instance-name: instance-config: instance-config.enable) config.services.oidc-reverse-proxy;
  package = import ./package.nix inputs;
  config-format = pkgs.formats.toml { };

  etc-layout = instance-name: instance-config: {
    "oidc-reverse-proxy.d/${instance-name}/appsettings.toml" = {
      text = (builtins.readFile ./src/appsettings.toml);
      mode = "0444";
    };
    "oidc-reverse-proxy.d/${instance-name}/appsettings.Production.toml" = {
      source = config-format.generate "config" instance-config.config;
      mode = "0444";
    };
  };
in
{
  options.services.oidc-reverse-proxy = lib.mkOption {
    description = ''
      Configure instances of the oidc-reverse-proxy.
    '';

    type = lib.types.attrsOf (
      lib.types.submodule (
        { instance-name, ... }:
        {
          options = {
            enable = lib.mkEnableOption "oidc-reverse-proxy for ${instance-name}";
            config = lib.mkOption {
              inherit (config-format) type;
              default = { };
              description = lib.mdDoc ''
                Configuration included in `appsettings.Prodution.toml`.
              '';
            };
            environmentFile = lib.mkOption {
              type = lib.types.nullOr lib.types.path;
              default = null;
              description = ''
                  Environment file as defined in {manpage}`systemd.exec(5)`
              '';
            };
          };
        }
      )
    );
  };

  config = {
    environment = lib.mapAttrs' (instance-name: instance-config: {
      name = "etc";
      value = instance-config;
    }) (lib.mapAttrs etc-layout enabled-instances);

    systemd.services = lib.mapAttrs' (instance-name: instance-config:
      {
        name = "oidc-reverse-proxy-${instance-name}";
        value = {
          description = "oidc-reverse-proxy-${instance-name}";
          serviceConfig = {
            Type = "simple";
            DynamicUser = true;
            ExecStart = "${package}/bin/oidc-reverse-proxy";
            EnvironmentFile = instance-config.environmentFile;
            WorkingDirectory = "/etc/oidc-reverse-proxy.d/${instance-name}";
          };
          wantedBy = [ "multi-user.target" ];
        };
      }) enabled-instances;
  };
}
