configuration items

ways to change how members are sorted

member_sorting = (member_type, access, name)
member_sorting = (member_type, access, -name)
member_sorting = (access, member_type, name)
member_sorting = (access, member_type, keyword) // maybe keyword is implied if name isn't specified? 

How to specify the order of member types within a C# type

member_type_ordering = delegates, field, property, constructor, method, event, interface, class, struct

How to sort member declarations with same type and access.

keyword_weights = static, readonly, required, identifier // sort by static first, readonly first, required first, then by name ASC
keyword_weights = static, readonly, -identifier // sort by static first, readonly first, then by name desc

