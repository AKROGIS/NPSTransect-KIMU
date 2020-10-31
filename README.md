# NPSTransect-KIMU

An ArcGIS Mobile application for doing transect based surveys by the National Park Service (NPS).
In particular, Kittlitz Murrelet observation in Glacier Bay National Park.

This application allowed the user to collect a tracklog and bird observtions along present survey transects.
It was designed for rapid data input on a moving map, but was specifically designed for a single
survey protocol and not a generic survey.  It worked offline (with a map cache), and saved the data
into a file geodatabase on the PC.

This application ran on a Windows PC.  It used Windows Presentation Framework (WPF),
and the [ArcGIS Mobile SDK](http://wiki.gis.com/wiki/index.php/ArcGIS_Mobile) which was deprecated
by esri shortly after this solution was deployed.

It also included a tool to export the data from the file geodatabase to a specific CSV format
used for analysis by follow on applications, and archiving in IRMA.

This solution was the foundation for the [Park Observer](https://github.com/AKROGIS/Observer) iPad application.
