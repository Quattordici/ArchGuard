# ARCH902: Invalid value in archie.json

A value is the wrong type, a namespace pattern or layer name is invalid, `mayDependOn` or `mustNotDependOn` names an unknown layer, a layer
is defined twice, a layer name in the list form has no `rootNamespace`, or two layers' namespaces match the same namespace equally well.
The message says which and points at it. Nothing is checked until it is fixed.
