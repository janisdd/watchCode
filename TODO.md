# TODO




- add option to suppress ok output (if a file hasn't changed we might not be interested in this)
  - so only output if we need auto update
  
- check cmd args...

- print help
  - add warning that every written new line is the system new line

- check if every config option is accessible via cmd arg & also applied!

- make readme

- remove unused snapshots ... option or auto e.g. if we change the range to another (known) range??

- remove reverse line reader... somehow combine top down & search method??


- maybe: reduce expressions if y includes x and x changed --> y changed! but if x does not change we don't know for y
- maybe: allow [int]- interval --> until file end
- maybe: somehow handle (automatically) if end of interval is bigger than total lines in file (now: output warning, ignore)
  - would involve updating doc files... which is critical
- maybe: check different encodings... of src file??

### if you are creazy

- somehow continue the bottom to top offset idea and maybe include git diff to find the lines??
