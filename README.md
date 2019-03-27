DMPModpackUpdater  
  
Place next to KSP and run. It will setup your GameData and automatically start KSP.  
  
Command line arguments:  
--stock: Reverts GameData to stock, implies --delete  
--delete: Delete mods that are not on the server, instead of only updating or adding  
--no-run: Do not attempt to start KSP  
--run=[ProgramName.exe]: Runs this program instead of trying to find and start KSP.  
--ksp-path=[path]: Run in specified folder, rather than the program location  
--ksp-args=\"args\": Run KSP with these arguments, example: --ksp-args="-force-d3d11 -popupwindow -dmp dmp://localhost:6702"  
--help: Displays this message  
