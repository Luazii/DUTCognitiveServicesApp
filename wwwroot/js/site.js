document.addEventListener('DOMContentLoaded', () => {
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const dropZonePrompt = document.getElementById('dropZonePrompt');
    const previewContainer = document.getElementById('previewContainer');
    const imagePreview = document.getElementById('imagePreview');
    const previewFileName = document.getElementById('previewFileName');
    const previewFileSize = document.getElementById('previewFileSize');
    const btnRemoveImage = document.getElementById('btnRemoveImage');
    const uploadForm = document.getElementById('uploadForm');
    const btnSubmit = document.getElementById('btnSubmit');

    if (!dropZone || !fileInput) return;

    // Trigger file dialog on drop zone click
    dropZone.addEventListener('click', (e) => {
        if (e.target.closest('#btnRemoveImage')) return;
        fileInput.click();
    });

    // File input change handler
    fileInput.addEventListener('change', () => {
        if (fileInput.files && fileInput.files[0]) {
            handleSelectedFile(fileInput.files[0]);
        }
    });

    // Drag & Drop handlers
    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.add('dragover');
        });
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('dragover');
        });
    });

    dropZone.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        if (dt.files && dt.files[0]) {
            fileInput.files = dt.files;
            handleSelectedFile(dt.files[0]);
        }
    });

    // Display image preview
    function handleSelectedFile(file) {
        if (!file.type.startsWith('image/')) {
            alert('Please select a valid image file (JPG, PNG, WEBP, BMP).');
            return;
        }

        const reader = new FileReader();
        reader.onload = (e) => {
            imagePreview.src = e.target.result;
            previewFileName.textContent = file.name;
            previewFileSize.textContent = formatBytes(file.size);

            dropZonePrompt.style.display = 'none';
            previewContainer.style.display = 'block';
        };
        reader.readAsDataURL(file);
    }

    // Remove selected image
    if (btnRemoveImage) {
        btnRemoveImage.addEventListener('click', (e) => {
            e.stopPropagation();
            fileInput.value = '';
            imagePreview.src = '#';
            previewContainer.style.display = 'none';
            dropZonePrompt.style.display = 'block';
        });
    }

    // Form submit loading animation
    if (uploadForm && btnSubmit) {
        uploadForm.addEventListener('submit', () => {
            if (fileInput.files && fileInput.files.length > 0) {
                const btnText = btnSubmit.querySelector('.btn-text');
                const spinner = btnSubmit.querySelector('.spinner');
                if (btnText && spinner) {
                    btnText.style.display = 'none';
                    spinner.style.display = 'inline-flex';
                    btnSubmit.disabled = true;
                }
            }
        });
    }

    function formatBytes(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
    }
});
