# Rule configuration for the repository's PowerShell gate.
# Applied by scripts/powershell-check.ps1, which ci.sh and format.sh both call.
@{
    IncludeDefaultRules = $true

    ExcludeRules = @(
        # Every .ps1 here is a console tool whose printed text is its interface,
        # exactly as the shell scripts use echo. Write-Output would put that text
        # on the pipeline instead, where a caller reading a function's return
        # value would receive the log lines along with it.
        'PSAvoidUsingWriteHost'
    )

    # PSUseBOMForUnicodeEncodedFile stays enabled deliberately. .gitattributes
    # normalizes text to UTF-8 without a BOM, and Windows PowerShell 5.1 reads
    # such a file as ANSI, so the only way to satisfy the rule here is to keep
    # these sources ASCII. That is the same conclusion AGENTS.md reaches about
    # emoji, enforced by the analyzer for .ps1 specifically.

    Rules = @{
        # The unity-open shims launch these scripts through powershell.exe, so
        # they have to keep parsing under Windows PowerShell 5.1 as well as the
        # pwsh 7 that runs the gate.
        PSUseCompatibleSyntax = @{
            Enable = $true
            TargetVersions = @('5.1', '7.0')
        }

        PSUseCompatibleCmdlets = @{
            compatibility = @(
                'desktop-5.1.14393.206-windows',
                'core-6.1.0-windows'
            )
        }

        # Formatting rules. Invoke-Formatter reads the same settings, so
        # ./format.sh rewrites files to exactly what the gate demands.
        PSPlaceOpenBrace = @{
            Enable = $true
            OnSameLine = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
        }

        PSPlaceCloseBrace = @{
            Enable = $true
            NewLineAfter = $false
            IgnoreOneLineBlock = $true
            NoEmptyLineBefore = $false
        }

        PSUseConsistentIndentation = @{
            Enable = $true
            Kind = 'space'
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
        }

        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckSeparator = $true
            CheckPipe = $true
        }
    }
}
