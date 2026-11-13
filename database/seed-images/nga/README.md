# Archive sample paintings (National Gallery of Art)

About 1,500 paintings used as archive sample data: two years of past, graded competitions with their covers,
award-winning entries, the public gallery and past exhibitions (`DbSeeder.Archive.cs`).

## Getting the pictures

The pictures (~450 MB, 1200 px on the long side) are **not in Git**. Download them once after cloning:

```powershell
powershell -ExecutionPolicy Bypass -File database\seed-images\nga\fetch-images.ps1
```

You can run the script again at any time. It skips pictures already downloaded and fetches only the missing or
broken ones. The next `dotnet run` then adds the archive to the database (also to an existing database, once).
Without the pictures the app runs normally, with only the main sample data.

To seed the archive into a database that already has it, delete the database
(`docker compose down -v`, then `docker compose up -d`) and run the app again.

## Credits and licence

All pictures come from the open data of the **National Gallery of Art, Washington**
(<https://github.com/NationalGalleryOfArt/opendata>). Only images marked open access are used: they are
public domain under **CC0**, free to use without permission.

`manifest.csv` lists every picture with its NGA object page (`page_url`) and the gallery's own description. The
titles are short labels made from that description for the sample data; they are not the artworks' real titles.

`build_manifest.py` rebuilds the list from `published_images.csv` of the open data repository. You only need it
to change the selection.
