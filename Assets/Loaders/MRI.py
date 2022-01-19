import os
import sys
import numpy as np
import nibabel as nib

input_folder = "../VolumeData/MRI/"
output_folder = "../Resources/VolumeRaw/MRI/"

in_file = os.path.join(input_folder, str(sys.argv[1]))
out_file = output_folder + str(sys.argv[2])

img = nib.load(in_file)

hdr_file = open(out_file + ".txt", "w")
hdr_file.write(str(img.header) + "\n" + "Shape: " + str(img.shape))
hdr_file.close()

arr = img.get_fdata()
save_arr = np.ascontiguousarray(arr)
np.save(out_file, save_arr)