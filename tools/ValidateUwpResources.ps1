[CmdletBinding()]
param(
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot '..\YouTube.Uwp\YouTube.Uwp.csproj'
}
$projectFile = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectDirectory = Split-Path -Parent $projectFile
$errors = [System.Collections.Generic.List[string]]::new()

# Maps the normalized YouTube i18nLanguages codes used to produce these translations
# to Windows-specific resource qualifiers. Do not use neutral tags in an AppX PRI.
$youtubeLocaleMap = [ordered]@{
    'af' = 'af-ZA'; 'am' = 'am-ET'; 'ar' = 'ar-SA'; 'as' = 'as-IN'
    'az' = 'az-Latn-AZ'; 'be' = 'be-BY'; 'bg' = 'bg-BG'; 'bn' = 'bn-BD'
    'bs' = 'bs-Latn-BA'; 'ca' = 'ca-ES'; 'cs' = 'cs-CZ'; 'da' = 'da-DK'
    'de' = 'de-DE'; 'de-DE' = 'de-DE'; 'el' = 'el-GR'; 'en' = 'en-US'
    'en-GB' = 'en-GB'; 'en-IN' = 'en-IN'; 'en-US' = 'en-US'
    'es' = 'es-ES'; 'es-419' = 'es-419'; 'es-ES' = 'es-ES'; 'es-US' = 'es-US'
    'et' = 'et-EE'; 'eu' = 'eu-ES'; 'fa' = 'fa-IR'; 'fi' = 'fi-FI'
    'fil' = 'fil-PH'; 'fr' = 'fr-FR'; 'fr-CA' = 'fr-CA'; 'gl' = 'gl-ES'
    'gu' = 'gu-IN'; 'he' = 'he-IL'; 'hi' = 'hi-IN'; 'hr' = 'hr-HR'
    'hu' = 'hu-HU'; 'hy' = 'hy-AM'; 'id' = 'id-ID'; 'is' = 'is-IS'
    'it' = 'it-IT'; 'ja' = 'ja-JP'; 'ka' = 'ka-GE'; 'kk' = 'kk-KZ'
    'km' = 'km-KH'; 'kn' = 'kn-IN'; 'ko' = 'ko-KR'; 'ky' = 'ky-KG'
    'lo' = 'lo-LA'; 'lt' = 'lt-LT'; 'lv' = 'lv-LV'; 'mk' = 'mk-MK'
    'ml' = 'ml-IN'; 'mn' = 'mn-MN'; 'mr' = 'mr-IN'; 'ms' = 'ms-MY'
    # Burmese (my) has no Microsoft Store-supported package language on Windows 10.
    'my' = $null; 'ne' = 'ne-NP'; 'nl' = 'nl-NL'; 'no' = 'nb-NO'
    'or' = 'or-IN'; 'pa' = 'pa-IN'; 'pl' = 'pl-PL'; 'pl-PL' = 'pl-PL'
    'pt' = 'pt-BR'; 'pt-PT' = 'pt-PT'; 'ro' = 'ro-RO'; 'ru' = 'ru-RU'
    'si' = 'si-LK'; 'sk' = 'sk-SK'; 'sl' = 'sl-SI'; 'sq' = 'sq-AL'
    'sr' = 'sr-Cyrl-RS'; 'sr-Latn' = 'sr-Latn-RS'; 'sv' = 'sv-SE'
    'sw' = 'sw-KE'; 'ta' = 'ta-IN'; 'te' = 'te-IN'; 'th' = 'th-TH'
    'tr' = 'tr-TR'; 'uk' = 'uk-UA'; 'ur' = 'ur-PK'; 'uz' = 'uz-Latn-UZ'
    'vi' = 'vi-VN'; 'zh-CN' = 'zh-Hans-CN'; 'zh-HK' = 'zh-Hant-HK'
    'zh-TW' = 'zh-Hant-TW'; 'zu' = 'zu-ZA'
}

function Add-ValidationError {
    param([string]$Message)

    $script:errors.Add($Message)
}

function New-OrdinalIgnoreCaseSet {
    return ,([System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase))
}

$expectedResources = New-OrdinalIgnoreCaseSet
foreach ($rawCode in $youtubeLocaleMap.Keys) {
    $locale = $youtubeLocaleMap[$rawCode]
    if ([string]::IsNullOrWhiteSpace($locale)) {
        continue
    }

    try {
        $culture = [Globalization.CultureInfo]::GetCultureInfo($locale)
        if ($culture.IsNeutralCulture) {
            Add-ValidationError "YouTube code '$rawCode' maps to neutral locale '$locale'; AppX resource locales must be specific."
        }
    }
    catch {
        Add-ValidationError "YouTube code '$rawCode' maps to unsupported Windows culture '$locale'."
    }

    [void]$expectedResources.Add("Strings\language-$locale\Resources.resw")
}

[xml]$project = Get-Content -LiteralPath $projectFile -Raw
$namespaceManager = [System.Xml.XmlNamespaceManager]::new($project.NameTable)
$namespaceManager.AddNamespace('msbuild', 'http://schemas.microsoft.com/developer/msbuild/2003')
$resourceNodes = @($project.SelectNodes('//msbuild:PRIResource', $namespaceManager))
$defaultLanguageNode = $project.SelectSingleNode('//msbuild:DefaultLanguage', $namespaceManager)

if ($null -eq $defaultLanguageNode -or [string]::IsNullOrWhiteSpace($defaultLanguageNode.InnerText)) {
    Add-ValidationError "The project must define DefaultLanguage."
    $defaultLanguage = $null
}
else {
    $defaultLanguage = $defaultLanguageNode.InnerText.Trim()
    try {
        $canonicalDefaultLanguage = [Globalization.CultureInfo]::GetCultureInfo($defaultLanguage).Name
        if (-not [string]::Equals($defaultLanguage, $canonicalDefaultLanguage, [System.StringComparison]::Ordinal)) {
            Add-ValidationError "DefaultLanguage '$defaultLanguage' must use canonical BCP-47 spelling '$canonicalDefaultLanguage'."
        }
    }
    catch {
        Add-ValidationError "DefaultLanguage '$defaultLanguage' is not a valid or supported BCP-47 language tag."
        $canonicalDefaultLanguage = $defaultLanguage
    }
}

if ($resourceNodes.Count -eq 0) {
    Add-ValidationError "No PRIResource items were found in $projectFile."
}

$registeredResources = New-OrdinalIgnoreCaseSet
$resourceEntries = @()
foreach ($resourceNode in $resourceNodes) {
    $include = $resourceNode.GetAttribute('Include').Replace('/', '\')
    if (-not $registeredResources.Add($include)) {
        Add-ValidationError "The PRIResource item '$include' is listed more than once."
        continue
    }

    $match = [regex]::Match(
        $include,
        '^Strings\\language-(?<tag>[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*)\\Resources\.resw$',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        Add-ValidationError "PRIResource '$include' must use Strings\language-<specific-BCP-47-tag>\Resources.resw."
        continue
    }

    $tag = $match.Groups['tag'].Value
    try {
        $culture = [Globalization.CultureInfo]::GetCultureInfo($tag)
        if ($culture.IsNeutralCulture) {
            Add-ValidationError "PRIResource '$include' has neutral language tag '$tag'; AppX resource locales must be specific."
        }
        # The catalog supplies Windows resource spelling. CultureInfo returns legacy
        # aliases (for example, zh-CN) for some script-qualified Windows tags.
        $canonicalTag = $tag
    }
    catch {
        Add-ValidationError "PRIResource '$include' has an invalid or unsupported BCP-47 language tag '$tag'."
        $canonicalTag = $tag
    }

    $resourceEntries += [pscustomobject]@{
        Include = $include
        Tag = $tag
        CanonicalTag = $canonicalTag
        FullPath = Join-Path $projectDirectory $include
    }
}

foreach ($expectedResource in $expectedResources) {
    if (-not $registeredResources.Contains($expectedResource)) {
        Add-ValidationError "The YouTube locale catalog requires PRIResource '$expectedResource'."
    }
}
foreach ($registeredResource in $registeredResources) {
    if (-not $expectedResources.Contains($registeredResource)) {
        Add-ValidationError "PRIResource '$registeredResource' is not in the YouTube locale catalog."
    }
}

foreach ($collision in $resourceEntries | Group-Object { $_.CanonicalTag.ToUpperInvariant() } | Where-Object Count -gt 1) {
    $paths = ($collision.Group.Include -join "', '")
    Add-ValidationError "The language qualifier '$($collision.Group[0].CanonicalTag)' maps to multiple PRI resources: '$paths'."
}

foreach ($entry in $resourceEntries) {
    if (-not (Test-Path -LiteralPath $entry.FullPath -PathType Leaf)) {
        Add-ValidationError "PRIResource '$($entry.Include)' does not exist on disk."
    }
}

$stringsDirectory = Join-Path $projectDirectory 'Strings'
if (-not (Test-Path -LiteralPath $stringsDirectory -PathType Container)) {
    Add-ValidationError "The Strings directory does not exist: $stringsDirectory."
}
else {
    foreach ($resourceDirectory in Get-ChildItem -LiteralPath $stringsDirectory -Directory) {
        if ($resourceDirectory.Name -notmatch '^language-[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$') {
            Add-ValidationError "Resource directory '$($resourceDirectory.Name)' must use the language-<specific-BCP-47-tag> convention."
        }
    }

    foreach ($resourceFile in Get-ChildItem -LiteralPath $stringsDirectory -Recurse -File -Filter Resources.resw) {
        $relativePath = $resourceFile.FullName.Substring($projectDirectory.Length).TrimStart('\')
        if (-not $registeredResources.Contains($relativePath)) {
            Add-ValidationError "Resource file '$relativePath' is not registered as a PRIResource."
        }
    }
}

$defaultEntry = @($resourceEntries | Where-Object CanonicalTag -eq $canonicalDefaultLanguage)
if (@($defaultEntry).Count -ne 1) {
    Add-ValidationError "Exactly one canonical $canonicalDefaultLanguage resource is required for the project's DefaultLanguage."
}
elseif (Test-Path -LiteralPath $defaultEntry.FullPath -PathType Leaf) {
    $defaultKeys = New-OrdinalIgnoreCaseSet
    try {
        [xml]$defaultResource = Get-Content -LiteralPath $defaultEntry.FullPath -Raw
        foreach ($dataNode in @($defaultResource.SelectNodes('/root/data'))) {
            $name = $dataNode.GetAttribute('name')
            if ([string]::IsNullOrWhiteSpace($name)) {
                Add-ValidationError "Resource '$($defaultEntry.Include)' contains an unnamed data element."
            }
            elseif (-not $defaultKeys.Add($name)) {
                Add-ValidationError "Resource '$($defaultEntry.Include)' contains duplicate key '$name'."
            }

            $valueNode = $dataNode.SelectSingleNode('value')
            if ($null -eq $valueNode -or [string]::IsNullOrWhiteSpace($valueNode.InnerText)) {
                Add-ValidationError "Resource '$($defaultEntry.Include)' contains an empty value for key '$name'."
            }
        }
        if ($defaultKeys.Count -ne 263) {
            Add-ValidationError "Resource '$($defaultEntry.Include)' must contain exactly 263 keys; found $($defaultKeys.Count)."
        }
    }
    catch {
        Add-ValidationError "Resource '$($defaultEntry.Include)' is not valid XML: $($_.Exception.Message)"
    }

    foreach ($entry in $resourceEntries) {
        if ($entry.Include -eq $defaultEntry.Include -or -not (Test-Path -LiteralPath $entry.FullPath -PathType Leaf)) {
            continue
        }

        $keys = New-OrdinalIgnoreCaseSet
        try {
            [xml]$resource = Get-Content -LiteralPath $entry.FullPath -Raw
            foreach ($dataNode in @($resource.SelectNodes('/root/data'))) {
                $name = $dataNode.GetAttribute('name')
                if ([string]::IsNullOrWhiteSpace($name)) {
                    Add-ValidationError "Resource '$($entry.Include)' contains an unnamed data element."
                }
                elseif (-not $keys.Add($name)) {
                    Add-ValidationError "Resource '$($entry.Include)' contains duplicate key '$name'."
                }

                $valueNode = $dataNode.SelectSingleNode('value')
                if ($null -eq $valueNode -or [string]::IsNullOrWhiteSpace($valueNode.InnerText)) {
                    Add-ValidationError "Resource '$($entry.Include)' contains an empty value for key '$name'."
                }
            }
        }
        catch {
            Add-ValidationError "Resource '$($entry.Include)' is not valid XML: $($_.Exception.Message)"
            continue
        }

        $missing = @($defaultKeys | Where-Object { -not $keys.Contains($_) } | Sort-Object)
        $unexpected = @($keys | Where-Object { -not $defaultKeys.Contains($_) } | Sort-Object)
        if ($missing.Count -gt 0) {
            Add-ValidationError "Resource '$($entry.Include)' is missing keys: $($missing -join ', ')."
        }
        if ($unexpected.Count -gt 0) {
            Add-ValidationError "Resource '$($entry.Include)' has keys absent from ${canonicalDefaultLanguage}: $($unexpected -join ', ')."
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error -ErrorAction Continue $_ }
    exit 1
}

Write-Host "Validated $($resourceEntries.Count) PRI resources for $($youtubeLocaleMap.Count) YouTube codes (one has no Store-supported package locale) with unique specific Windows BCP-47 qualifiers and 263 nonempty keys matching en-US."
