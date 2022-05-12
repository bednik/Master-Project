# Master-Project
This project explores volume rendering optimizations on Microsoft's HoloLens 2.

Use NuGet for Unity to import MathNet.

## Limitations
- Unshaded. To make it shaded, calculate the normals from the volume texture and put them in the volume texture (RGBA32 texture instead of R8)
    - Could also add a moveable light
    - This would increase the quality of the final render
- Only preset transfer functions. Idea: Add a panel where one can interactively change the transfer function, maybe even in runtime if it is fast enough
- Requires preprocessing of volumes in both Python and the Unity Editor to make a 3D texture
- Still on the slow side on HoloLens
- More optimizations could be explored. For example, checking if Morton-order 3D matrices (better spatial locality) are better for the HoloLens 2 texture mapper could be an interesting experiment
    - Not completely sure if this matters, but it could be interesting to see if changing the shape of the transfer function has an impact. At this point it is 1D, but could the texture mapper prefer a square shape? Filtering may be problematic
    - Is it possible to reduce the amount of branching when performing chebyshev skipping?
    - At some point HoloLens 3 will be out. Crossing our fingers for hardware accelerated volume rendering!