# cls is a command line tool to navigate through files and perform file operations (file manager yea you got it)


## Navigation

- `j/k` --- Up and Down 
- `..` --- Parent directory
- `Enter` --- open file (xdg-open file) / navigate to directory

## Selection
- `Tab` --- toggle select multiple files
- Single file operations work without Tab

## Delete
- First `d`  --- highlights red (pending)
- Second `d` --- deletes. if Tab selected files, deletes all marked

## Cut
- `x` --- dims selected file(s), marks for move
- `p` --- moves to current directory

## Copy
- `c` --- dims the selected file(s), marks for copy
- `p` --- copies to current directory

## Quit
- `q` --- exit

## Escape behavior:

- `Esc` --- clears everything. All selections, pending cut/copy/delete state. Back to normal.
Tab on already selected file — toggles it off (deselects single file)


## Rename
- `r` --- enables file renaming mode and lets you type new name of the file and press `Enter` to rename the file
