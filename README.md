# VoxelEngine
Research into Minecraft-style voxel engines. Done primarily in Unity as the aim was to explore the data structures and algorithms more so than the lower-level internals of a game engine.

My apologies to anyone finding this repository. While I do think some portions of this project may be useful to some, Im generally not happy with the code. 

I also originally had no intent of releasing this publically so please excuse the nonsensical commit summaries. Maybe sometime in the future Ill pick this project back up or reimplement it in a cleaner and more structured way.

## Ramblings
Never did figure out how to speed up lighting calculations. I mostly used naive BFS and made very few attempts top optimize it. This is definitely something I would like to revisit in the future. 

I ran into an issue where lighting calculations were heavily dependent on neigboring chunks which resulted in a lot more work being done per chunk lighting update than expected. I think this could be solved by utilizing a tiered loading system  to ensure chunks are only lit when the next tier or ring out from the player has been loaded. I could then skip completely recalculating propagation from neighboring chunks if their light maps have not been flagged as dirty. 

Had some struggles with biome-based noise generation. Right now, biome boundaries are harsh and very little blending is done. Im pretty sure Minecraft uses transition biomes to help with this issue but Im also fairly certain I did something wrong with the bilinear filtering.

## Credits
Textures used are from the Pixel Perfection texture pack by XSSheep.
You can find the Minecraft Forum post here: 
[Pixel Perfection](https://www.minecraftforum.net/forums/mapping-and-modding-java-edition/resource-packs/1242533-pixel-perfection-now-with-polar-bears-1-11).
