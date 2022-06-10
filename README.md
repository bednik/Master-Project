# Master-Project
This project explores volume rendering optimizations on Microsoft's HoloLens 2.

The most interesting scripts (Apart from the shaders) are volumebuilder.cs and volumerendercontroller.cs. These contain some necessary preprocessing for the optimizations. Most of the code for occupancy map and Chebyshev distance map is inspired by Deakin and Knackstedt's paper on Chebyshev distance maps: https://link.springer.com/article/10.1007/s41095-019-0155-y
In addition, some is adapted for C# and HLSL from Deakin's open-source volume renderer: https://github.com/LDeakin/VkVolume

Use NuGet for Unity to import MathNet.
Use the editor scripts to convert the data to 3D textures, or make a much better script because it is a bit impractical to do it beforehand. For example, it would be nice to simply input an mhd file for CT or an h5 file for ultrasound and just have it work.

## Limitations
- Only preset transfer functions. Idea: Add a panel where one can interactively change the transfer function and click on a button to apply, maybe even in runtime if it is fast enough
- Requires preprocessing of volumes in both Python and the Unity Editor to make a 3D texture
- Still on the slow side on HoloLens
- More optimizations could be explored. For example, checking if Morton-order 3D matrices (better spatial locality) are better for the HoloLens 2 texture mapper could be an interesting experiment
- Is it possible to reduce the amount of branching when performing chebyshev skipping?
- At some point HoloLens 3 will be out. Crossing our fingers for hardware accelerated volume rendering!
