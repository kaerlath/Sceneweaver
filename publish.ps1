param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    $repository = 'kaerlath/Sceneweaver'
    $repositoryUrl = 'https://github.com/kaerlath/Sceneweaver.git'
    $releaseTag = 'v0.1.0'
    # This checkout may have been created by Codex's sandbox account.
    # Trust only this exact directory, only for the Git invocations below.
    $trustedDirectory = 'safe.directory=' + $PSScriptRoot.Replace('\', '/')

    gh auth status --hostname github.com
    if ($LASTEXITCODE -ne 0) {
        gh auth login --hostname github.com --git-protocol https --web
        if ($LASTEXITCODE -ne 0) { throw 'GitHub sign-in did not finish.' }
    }
    $account = gh api user --jq .login
    if ($LASTEXITCODE -ne 0 -or $account.Trim() -ne 'kaerlath') { throw 'Sign in to GitHub CLI as kaerlath before publishing.' }
    $repositoryInfo = gh repo view $repository --json nameWithOwner,visibility | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Cannot access kaerlath/Sceneweaver.' }
    if ($repositoryInfo.visibility -ne 'PUBLIC') { throw 'The repository is not public. Change its visibility in GitHub before running this script.' }

    $changes = git -c $trustedDirectory status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the local repository.' }
    if ($changes) { throw 'There are uncommitted changes. Review and commit them before publishing.' }
    $branch = git -c $trustedDirectory branch --show-current
    if ($branch -ne 'main') { throw 'Switch to the main branch before publishing.' }

    $remotes = @(git -c $trustedDirectory remote)
    if ($remotes -contains 'origin') {
        $origin = git -c $trustedDirectory remote get-url origin
        if ($origin -notin @($repositoryUrl, 'https://github.com/kaerlath/Sceneweaver', 'git@github.com:kaerlath/Sceneweaver.git')) {
            throw "Origin points somewhere else: $origin"
        }
    } else {
        git -c $trustedDirectory remote add origin $repositoryUrl
        if ($LASTEXITCODE -ne 0) { throw 'Could not add the GitHub remote.' }
    }

    # Use GitHub CLI's credential helper only for this push; do not change global Git settings.
    git -c $trustedDirectory -c 'credential.https://github.com.helper=' -c 'credential.https://github.com.helper=!gh auth git-credential' push --set-upstream origin main
    if ($LASTEXITCODE -ne 0) { throw 'Push failed. No force push was attempted; inspect any existing remote commits before retrying.' }
    $commit = git -c $trustedDirectory rev-parse HEAD

    & (Join-Path $PSScriptRoot 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Build or tests failed; no release was published.' }
    $changesAfterBuild = git -c $trustedDirectory status --porcelain
    if ($changesAfterBuild) { throw 'Build changed tracked source or created untracked files. Review and commit those changes before publishing a release.' }
    git -c $trustedDirectory archive --format=zip --output=dist/Sceneweaver-source-0.1.0.zip HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not package the committed source.' }

    $releaseList = gh api "repos/$repository/releases" --paginate | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Cannot check existing releases.' }
    if (@($releaseList | Where-Object tag_name -EQ $releaseTag).Count -gt 0) {
        Write-Host "Release $releaseTag already exists. Existing release assets were left unchanged."
        gh release view $releaseTag --repo $repository
        return
    }
    gh release create $releaseTag dist/Sceneweaver-0.1.0.zip dist/Sceneweaver-source-0.1.0.zip --repo $repository --target $commit --prerelease --title 'Sceneweaver 0.1.0 - Development preview' --notes-file docs/RELEASE-0.1.0.md
    if ($LASTEXITCODE -ne 0) { throw 'Source was pushed, but release publication failed. Rerun this script to retry.' }
    gh release view $releaseTag --repo $repository --json url,isPrerelease,assets
    if ($LASTEXITCODE -ne 0) { throw 'Release was submitted, but verification failed. Check the GitHub Releases page.' }
    Write-Host 'Published https://github.com/kaerlath/Sceneweaver'
} finally {
    Pop-Location
}
