# VoxelEngine
Research into Minecraft-style voxel engines. Done primarily in Unity as the aim was to explore the data structures and algorithms more so than the lower-level internals of a game engine.

## Current Features
- Multithreaded chunk pipeline.
- Rough atlas generation at runtime from individual textures.
- Minecraft-style voxel lighting.

## Planned Features
- Smarter lighting and meshing routines.
- World saving/loading with some sort of efficient data structure.
- Basic mobs and interactable block entities.

## Stretch Goals
- Better world generation with caves, foliage, etc.
- Basic multiplayer support with a dedicated server.
- GPU-accelerated lighting/meshing (if possible).

## Update
I've since picked this project back up with the aim of improving performance and fixing all the major bugs that currently exist.

## Ramblings
Never did figure out how to speed up lighting calculations. I mostly used naive BFS and made very few attempts to optimize it. This is definitely something I would like to revisit in the future. 

I ran into an issue where lighting calculations were heavily dependent on neigboring chunks which resulted in a lot more work being done per chunk lighting update than expected. I think this could be solved by utilizing a tiered loading system  to ensure chunks are only lit when the next tier or ring out from the player has been loaded. I could then skip completely recalculating propagation from neighboring chunks if their light maps have not been flagged as dirty. 

Had some struggles with biome-based noise generation. Right now, biome boundaries are harsh and very little blending is done. Im pretty sure Minecraft uses transition biomes to help with this issue but Im also fairly certain I did something wrong with the bilinear filtering.

## Credits
Textures used are from the Pixel Perfection texture pack by XSSheep.
You can find the Minecraft Forum post here: 
[Pixel Perfection](https://www.minecraftforum.net/forums/mapping-and-modding-java-edition/resource-packs/1242533-pixel-perfection-now-with-polar-bears-1-11).
