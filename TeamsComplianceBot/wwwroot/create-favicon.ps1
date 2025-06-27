# Create a simple 16x16 favicon.ico
# This is a minimal ICO file header for a 16x16 favicon
# ICO file format: Header (6 bytes) + Directory Entry (16 bytes) + Image Data
$bytes = @(
    # ICO Header (6 bytes)
    0x00, 0x00,  # Reserved (must be 0)
    0x01, 0x00,  # Image type (1 = ICO)
    0x01, 0x00,  # Number of images (1)
    
    # Directory Entry (16 bytes)
    0x10,        # Width (16 pixels)
    0x10,        # Height (16 pixels) 
    0x00,        # Color count (0 = no palette)
    0x00,        # Reserved
    0x01, 0x00,  # Color planes (1)
    0x20, 0x00,  # Bits per pixel (32)
    0x84, 0x00, 0x00, 0x00,  # Size of image data (132 bytes)
    0x16, 0x00, 0x00, 0x00   # Offset to image data (22 bytes)
) + @(
    # 16x16 32-bit RGBA image data (simplified)
    # This creates a simple blue square favicon
    (1..256 | ForEach-Object { 
        if ($_ % 4 -eq 1) { 0x80 }      # Blue
        elseif ($_ % 4 -eq 2) { 0x40 }  # Green  
        elseif ($_ % 4 -eq 3) { 0x00 }  # Red
        else { 0xFF }                   # Alpha
    })
)

[System.IO.File]::WriteAllBytes("favicon.ico", $bytes)
