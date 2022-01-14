import h5py
import numpy as np
import sys
import os

input_folder = "../VolumeData/US/"
output_folder = "../Resources/VolumeRaw/US/"

f = h5py.File(input_folder + str(sys.argv[1]), 'r')
savelocation = output_folder + str(sys.argv[2])

# Fetch cartesian volumes
cartesian_volumes = f["CartesianVolumes"]

if not os.path.exists(savelocation):
    os.makedirs(savelocation)

for key in cartesian_volumes.keys():
    v = cartesian_volumes[str(key)]
    np.save(savelocation + "/" + str(key), v)
