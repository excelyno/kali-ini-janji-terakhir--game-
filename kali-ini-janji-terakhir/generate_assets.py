import os
import math
from PIL import Image, ImageDraw

def create_sprite(name, color, size=(48, 48), pattern=None):
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # Base
    draw.rectangle([0, 0, size[0]-1, size[1]-1], outline=(0,0,0,255), fill=color, width=2)
    
    # Highlights & shadows for 2.5D feel
    draw.rectangle([2, 2, size[0]-3, size[1]/2], fill=tuple(min(255, c + 40) if i < 3 else c for i, c in enumerate(color)))
    
    if pattern == 'checker':
        for y in range(0, size[1], 8):
            for x in range(0, size[0], 8):
                if (x + y) % 16 == 0:
                    draw.rectangle([x, y, x+7, y+7], fill=tuple(max(0, c - 20) if i < 3 else c for i, c in enumerate(color)))
    elif pattern == 'dots':
        for i in range(5):
            draw.ellipse([size[0]/2 - 10 + i*4, size[1]/2 - 10, size[0]/2 - 6 + i*4, size[1]/2 - 6], fill=(0,0,0,100))
    elif pattern == 'water':
        for y in range(0, size[1], 16):
            draw.line([(0, y), (size[0], y)], fill=(255,255,255,150), width=2)
            
    path = os.path.join("Assets", "FileAssets", name + ".png")
    img.save(path)
    print(f"Generated: {path}")

os.makedirs(os.path.join("Assets", "FileAssets"), exist_ok=True)

# Characters & Animals
create_sprite("Player_Idle", (50, 150, 255, 255), (48, 64))
create_sprite("Chicken_Idle", (255, 255, 255, 255), (32, 32))
create_sprite("Cow_Idle", (220, 220, 220, 255), (64, 48), pattern='checker')

# Environment & Objects
create_sprite("Tree_Pine", (34, 139, 34, 255), (64, 128))
create_sprite("Tree_Apple", (50, 205, 50, 255), (64, 128), pattern='dots')
create_sprite("Rock_Normal", (169, 169, 169, 255), (48, 48))
create_sprite("House_Main", (205, 133, 63, 255), (256, 256))
create_sprite("Fence_Wood", (139, 69, 19, 255), (48, 48))
create_sprite("Shipping_Bin", (160, 82, 45, 255), (64, 64))

# Terrain & Tiles
create_sprite("Soil_Tilled", (139, 69, 19, 255), (48, 48), pattern='checker')
create_sprite("Grass_Base", (124, 252, 0, 255), (48, 48), pattern='checker')
create_sprite("Water_Tile", (65, 105, 225, 255), (48, 48), pattern='water')
create_sprite("Dirt_Path", (210, 180, 140, 255), (48, 48))

# Crops
create_sprite("Crop_Seed", (139, 69, 19, 255), (48, 48), pattern='dots')
create_sprite("Crop_Sprout", (144, 238, 144, 255), (48, 48))
create_sprite("Crop_Tomato", (255, 99, 71, 255), (48, 48))
create_sprite("Crop_Corn", (255, 215, 0, 255), (48, 48))

# Tools
create_sprite("Tool_Hoe", (192, 192, 192, 255), (32, 32))
create_sprite("Tool_WateringCan", (70, 130, 180, 255), (32, 32))
create_sprite("Tool_Axe", (119, 136, 153, 255), (32, 32))
create_sprite("Tool_Pickaxe", (105, 105, 105, 255), (32, 32))

print("Asset generation complete!")
