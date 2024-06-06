# Snap Image Tagging
Allows the user to quickly reverse image search a folder of JPG pictures and add useful EXIF tags in the image metadata.
## Reverse Image Search
While using the Reverse Image Search mode:
- Submitting a URL will create a **[website]:[username]** tag for several common social media websites
- Submitting a name will create a **name:[username]** tag otherwise
- Skipping an image will add an **unknownName** tag to the image
- Left-clicking on one of the reverse image search results will create a similar tag from the result's URL
- Right-clicking on an image in Reverse Image Search mode will copy the image's URL to the clipboard
- Selecting an file on your computer that already has this program's name/socials tags will copy those tags.

Not checking a Reverse Image Search option will apply tags (selected from those described below) to the selected directory level's images without further user input. 
## Included tags
- "Date Taken" generated from system modified time if tag is absent, to preserve organization in cloud storage
- Event name, gathered from the images' parent folder
- Custom fixed tags
