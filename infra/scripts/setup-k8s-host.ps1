# =============================================================================
# Central K8s Host Setup — Run as Administrator
# =============================================================================
# This script performs the admin-privileged steps needed for K8s cluster setup:
#   1. Adds VMnet2 host-only network (192.168.56.0/24)
#   2. Installs Vagrant VMware Utility
#   3. Installs GitHub CLI
#   4. Verifies all prerequisites
# =============================================================================

$ErrorActionPreference = "Stop"

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Right-click PowerShell > Run as Administrator, then re-run this script."
    exit 1
}

Write-Host "=== Central K8s Host Setup ===" -ForegroundColor Cyan

# --- Step 1: VMware VMnet2 (192.168.56.0/24) ---
Write-Host "`n[1/3] Configuring VMware VMnet2 (192.168.56.0/24)..." -ForegroundColor Yellow

$vnetlib = "C:\Program Files (x86)\VMware\VMware Workstation\vnetlib.exe"
if (Test-Path $vnetlib) {
    & $vnetlib -- stop dhcp
    & $vnetlib -- stop nat
    & $vnetlib -- add adapter vmnet2
    & $vnetlib -- set vnet vmnet2 addr 192.168.56.0
    & $vnetlib -- set vnet vmnet2 mask 255.255.255.0
    & $vnetlib -- set adapter vmnet2 addr 192.168.56.1
    & $vnetlib -- update adapter vmnet2
    & $vnetlib -- update dhcp vmnet2
    & $vnetlib -- start dhcp
    & $vnetlib -- start nat
    Write-Host "  VMnet2 configured. Verifying..." -ForegroundColor Green

    Start-Sleep -Seconds 3
    $adapter = Get-NetAdapter | Where-Object { $_.Name -like "*VMnet2*" }
    if ($adapter) {
        Write-Host "  VMnet2 adapter found: $($adapter.Name) - $($adapter.Status)" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: VMnet2 adapter not detected. Open VMware > Edit > Virtual Network Editor" -ForegroundColor Red
        Write-Host "           Add Network > VMnet2 > Host-only > Subnet: 192.168.56.0, Mask: 255.255.255.0" -ForegroundColor Red
    }
} else {
    Write-Host "  WARNING: vnetlib.exe not found. Configure manually in VMware Virtual Network Editor." -ForegroundColor Red
}

# --- Step 2: Vagrant VMware Utility ---
Write-Host "`n[2/3] Installing Vagrant VMware Utility..." -ForegroundColor Yellow

$utilityPath = "C:\Program Files (x86)\HashiCorp\Vagrant VMware Utility"
if (Test-Path $utilityPath) {
    Write-Host "  Already installed at $utilityPath" -ForegroundColor Green
} else {
    $msiPath = "$env:TEMP\vagrant-vmware-utility.msi"
    if (-not (Test-Path $msiPath)) {
        Write-Host "  Downloading..."
        Invoke-WebRequest -Uri "https://releases.hashicorp.com/vagrant-vmware-utility/1.0.23/vagrant-vmware-utility_1.0.23_windows_amd64.msi" -OutFile $msiPath
    }
    Write-Host "  Installing..."
    Start-Process msiexec.exe -ArgumentList "/i `"$msiPath`" /qn /norestart" -Wait -NoNewWindow
    if (Test-Path $utilityPath) {
        Write-Host "  Installed successfully." -ForegroundColor Green
    } else {
        Write-Host "  WARNING: Installation may have failed. Try running the MSI manually." -ForegroundColor Red
    }
}

# Start the utility service
$svc = Get-Service -Name "VagrantVMwareUtility" -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne "Running") { Start-Service "VagrantVMwareUtility" }
    Write-Host "  Vagrant VMware Utility service is running." -ForegroundColor Green
} else {
    Write-Host "  WARNING: VagrantVMwareUtility service not found." -ForegroundColor Yellow
}

# --- Step 3: GitHub CLI ---
Write-Host "`n[3/3] Installing GitHub CLI..." -ForegroundColor Yellow

$ghPath = Get-Command gh -ErrorAction SilentlyContinue
if ($ghPath) {
    Write-Host "  Already installed: $(gh --version | Select-Object -First 1)" -ForegroundColor Green
} else {
    Write-Host "  Installing via winget..."
    winget install --id GitHub.cli --accept-source-agreements --accept-package-agreements --scope machine
}

# --- Summary ---
Write-Host "`n=== Setup Complete ===" -ForegroundColor Cyan
Write-Host "Next steps:"
Write-Host "  1. Verify VMnet2: ipconfig | findstr VMnet2"
Write-Host "  2. Start K8s VMs: cd infra\vagrant && vagrant up"
Write-Host "  3. Copy kubeconfig: vagrant ssh k8s-master -c 'sudo cat /etc/kubernetes/admin.conf' > ~/.kube/central-local.conf"
