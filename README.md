# SkinAndSpay
Skin Handling And Custom Spray Decals.<br><br>
Note: Plugin Wil fallback to mode where it skins the decal 2 seconds after the spray on oxide versions that are missing OnSprayCreate Hook<br>
Make sure you dont move for the 3 seconds it takes when using oxide version 2.0.5532.0.<br><br>

//Chat commands
<br>
        /Spray skinid                  =   Sets spraycan to use custom skinid or returns to default if already set.<br>
        /sprayresize size              =   Resizes the spray being looked at.<br>
        /spraysize size                =   Custom sprays will come out at this size.<br>
        /SkinAndSpay skinid            =   Just reskin with provided skinid.<br>
        /SkinAndSpay skinid "new name" =   Reskin and change name of item.<br>
        <br>
//Permission to use command<br>
        SkinAndSpay.use    (To Chance Spray Skins)<br>
        SkinAndSpay.skin   (To Use the Chat Command To Skin Items)<br>
        SkinAndSpay.size   (To Use the Resize Commands)<br><br>
        
# SkinAndSpay Creating Skins
        Demo Video: https://www.youtube.com/watch?v=ti-cUEtf_u4
<br>SprayCan Skins Only Work If They Are Tagged As "Spray Can Decal".<br>
The built in workshop editor in rust doesnt allow this so youll need to create the skins by hand.<br>
Then use https://github.com/nihilocrat/SteamWorkshopUploader to upload it.<br><br>
<a href="https://github.com/bmgjet/SkinAndSpay/raw/main/SteamWorkshopUploader.zip" target="_blank">SteamWorkshopUploader.zip</a> is included in this repo and is preset up with 1 skin to use as a template.<br>
Watch the video to see how to use it.<br>https://www.youtube.com/watch?v=1DV7BDwbJAw<br><br>

# Using Skins Plugin As GUI
You can use another plugih such as Skins to work as a GUI for allowing players to switch between skins.<br>
Here is a example you can add to oxide/config/Skins.json<br>
<p style="padding-left: 80px;">{<br />"Item Shortname": "spraycan",<br />"Skins": [<br />2816639887,<br />2816652031,<br />2816648654,<br />2816580876,<br />2816218649,<br />2816218582,<br />2816218516,<br />2816218452,<br />2816676408,<br />2816675297,<br />2816673852,<br />2816773393,<br />2816763883,<br />2816766036,<br />2816768996<br />]<br />},</p>

