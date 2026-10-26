using LiveStream.SdkTool;

namespace LiveStream.SdkTool;

/// <summary>
/// Autocompletado del CLI para PowerShell, Bash y Zsh. Los guiones están escritos con la misma lista de órdenes
/// que <see cref="Help.CommandList"/>: si se añade una orden, se actualizan los tres guiones juntos.
/// </summary>
public static partial class CliCommands
{
    private const string VerbList =
        "init init-templates validate build pack install list info enable disable uninstall doctor " +
        "keygen sign verify publish yank unyank backup config completions version";
    private const string OptionList =
        "--id --name --template --author --version --description --project -o --key --public " +
        "--public-key --private --server --dev --force --yes --json --quiet --verbose --no-interactive";
    private const string TemplateList = "basic graphics sources filters integrations tools";

    /// <summary>Imprime el guion de autocompletado para la shell pedida con indicaciones de instalación.</summary>
    public static int Completions(Ui ui, string[] rest)
    {
        var shell = Args.Positional(rest, 0, "Ejemplo: lsplugin completions pwsh   (o bash, zsh)");
        switch (shell.ToLowerInvariant())
        {
            case "pwsh" or "powershell":
                ui.Line(PowerShellScript);
                break;
            case "bash":
                ui.Line(BashScript);
                break;
            case "zsh":
                ui.Line(ZshScript);
                break;
            default:
                throw new UsageException($"Shell no soportada: «{shell}».", "Válidas: pwsh, bash, zsh.");
        }
        return ExitCodes.Ok;
    }

    private const string PowerShellScript = """
        # Autocompletado de lsplugin para PowerShell (PowerShell 7+/5.1 con Register-ArgumentCompleter).
        # Instalarlo para siempre:  lsplugin completions pwsh >> $PROFILE
        Register-ArgumentCompleter -Native -CommandName lsplugin -ScriptBlock {
            param($wordToComplete, $commandAst)
            $verbs = 'init init-templates validate build pack install list info enable disable uninstall doctor keygen sign verify publish yank unyank backup config completions version' -split ' '
            $options = '--id --name --template --author --version --description --project -o --key --public --public-key --private --server --dev --force --yes --json --quiet --verbose --no-interactive' -split ' '
            $previous = $commandAst.CommandElements | Select-Object -ExpandProperty ToString -ErrorAction SilentlyContinue
            $last = if ($previous.Count -ge 2) { $previous[$previous.Count - 2] } else { '' }
            if ($last -eq '--template') {
                'basic graphics sources filters integrations tools' -split ' ' | Where-Object { $_ -like "$wordToComplete*" }
                return
            }
            if ($wordToComplete -like '-*') {
                $options | Where-Object { $_ -like "$wordToComplete*" }
            } else {
                $verbs | Where-Object { $_ -like "$wordToComplete*" }
            }
        }
        """;

    private const string BashScript = """
        # Autocompletado de lsplugin para bash.
        # Instalarlo para siempre:  lsplugin completions bash >> ~/.bashrc
        _lsplugin_completions() {
            local cur prev
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"
            if [[ "$prev" == "--template" ]]; then
                COMPREPLY=( $(compgen -W "basic graphics sources filters integrations tools" -- "$cur") )
                return 0
            fi
            if [[ "$cur" == -* ]]; then
                COMPREPLY=( $(compgen -W "--id --name --template --author --version --description --project -o --key --public --public-key --private --server --dev --force --yes --json --quiet --verbose --no-interactive" -- "$cur") )
            else
                COMPREPLY=( $(compgen -W "init init-templates validate build pack install list info enable disable uninstall doctor keygen sign verify publish yank unyank backup config completions version" -- "$cur") )
            fi
            return 0
        }
        complete -F _lsplugin_completions lsplugin
        """;

    private const string ZshScript = """
        #compdef lsplugin
        # Autocompletado de lsplugin para zsh.
        # Instalarlo para siempre:  mkdir -p ~/.zfunc; lsplugin completions zsh > ~/.zfunc/_lsplugin
        # y añade a ~/.zshrc:       fpath=(~/.zfunc $fpath); autoload -Uz compinit && compinit
        _lsplugin() {
            local -a verbs options templates
            verbs=(init init-templates validate build pack install list info enable disable uninstall doctor
                keygen sign verify publish yank unyank backup config completions version)
            options=(--id --name --template --author --version --description --project -o --key --public
                --public-key --private --server --dev --force --yes --json --quiet --verbose --no-interactive)
            templates=(basic graphics sources filters integrations tools)
            _arguments \
                -C '1: :{ _describe "orden" verbs }' \
                '*: :{ _alternative "opciones: :->opts" ; _values "--template" templates }'
            case $state in
                opts) compadd -a options ;;
            esac
        }
        _lsplugin "$@"
        """;
}
