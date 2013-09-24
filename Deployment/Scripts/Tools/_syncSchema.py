# ---------------------------------------------------------------------------
# _syncSchema.py
# Created on: 2011-06-13 14:43:45.00000
#   (generated by ArcGIS/ModelBuilder)
# Description: 
# ---------------------------------------------------------------------------

print "# Syncing mobile map schema"

# Set the necessary product code
#import arceditor
import arcinfo

# Import arcpy module
import arcpy

# Local variables:
Murrelets__sync__mxd = r"C:\KIMU\Murrelets (sync).mxd"
P_murrelet = r"C:\KIMU\MobileProject"

print "# Reading:",Murrelets__sync__mxd
print "# Writing:",P_murrelet

# Process: Create Mobile Map
arcpy.CreateMobileCache_mobile(Murrelets__sync__mxd, P_murrelet)

print "# Created Mobile Schema!"
