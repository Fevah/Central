/* Central — App JS */

// Tab switching
function showTab(name) {
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.add('hidden'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  const panel = document.getElementById('tab-' + name);
  if (panel) panel.classList.remove('hidden');
  // Find and activate the button that triggered this tab
  document.querySelectorAll('.tab-btn').forEach(btn => {
    if (btn.getAttribute('onclick') && btn.getAttribute('onclick').includes("'" + name + "'")) {
      btn.classList.add('active');
    }
  });
}

// Copy config to clipboard
function copyConfig() {
  const pre = document.getElementById('config-output');
  if (!pre) return;
  navigator.clipboard.writeText(pre.textContent).then(() => {
    const toast = document.getElementById('copy-toast');
    if (toast) {
      toast.classList.remove('hidden');
      setTimeout(() => toast.classList.add('hidden'), 2000);
    }
  });
}

// Toggle word wrap on config block
function toggleWrap() {
  const pre = document.getElementById('config-output');
  if (pre) pre.classList.toggle('wrap');
}

// File input label update
function updateLabel(input, labelId) {
  const label = document.getElementById(labelId);
  if (!label) return;
  const files = Array.from(input.files);
  if (files.length === 0) return;
  if (files.length === 1) {
    label.textContent = files[0].name;
  } else {
    label.textContent = `${files.length} files selected`;
  }
}

// Drag and drop for file inputs
document.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('.file-drop').forEach(drop => {
    drop.addEventListener('dragover', e => {
      e.preventDefault();
      drop.style.borderColor = 'var(--accent)';
      drop.style.background = 'rgba(88,166,255,0.08)';
    });
    drop.addEventListener('dragleave', () => {
      drop.style.borderColor = '';
      drop.style.background = '';
    });
    drop.addEventListener('drop', e => {
      e.preventDefault();
      drop.style.borderColor = '';
      drop.style.background = '';
      const input = drop.querySelector('input[type=file]');
      if (input && e.dataTransfer.files.length) {
        input.files = e.dataTransfer.files;
        const labelId = input.id.replace('-files', '-label').replace('-file', '-label');
        updateLabel(input, labelId);
      }
    });
  });
});
