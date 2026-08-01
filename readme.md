# CSharp Majordomo

This package is inspired by the [CodeMaid extension for Visual Studio][1]. That extension has served me well for many years but 
I needed a way to enforce some of its features when teammates don't have it installed.

This package is a work in progress and all its rules should be assumed to be in flux.

## Sorting .editorconfig 

You can sort files and members within types by many different criteria including:

### member types
* delegate
* field
* property
* constructor
* method
* event
* interface
* class
* struct

### visibility
* public
* private
* protected
* internal

### All modifiers
* static
* readonly
* abstract
* override
* sealed
* unsafe
* ...


This is done by defining a csv of the above criteria to sort by with the first being the highest priority and latter being "then by" lower priorities.
Place this in your `.editorconfig` file:

```
CSharpMajordomo.member_sort_order = delegate, field, property, constructor, destructor, operator, method, indexer, event, enum, interface, record, class, struct, public, protected, internal, private, static, readonly, identifier
```

### Descending order

You can sort descending by prefixing the sort criteria with a '-'.

```
CSharpMajordomo.member_sort_order = public, private, -identifier
```


## Blank lines .editorconfig

You can also configure the number of blank lines between members and member groups. Place this in your `.editorconfig` file:


```
# always 1 blank line between members, except fields do not need blank lines. Finally properties are excempt and can have as many as they want.
CSharpMajordomo.blank_lines_between_members = 1, field:0, property:-1

# there must always be 1 blank line between different groups of member types
CSharpMajordomo.blank_lines_between_member_groups = 1
```

[1]: https://marketplace.visualstudio.com/items?itemName=SteveCadwallader.CodeMaid