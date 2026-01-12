# Translations

ETS expects translations to be included as `Languages`/`TranslationUnit`/`TranslationElement` elements under the `Manufacturer` tag. However this is problematic for OpenKNX as the config files extensively use dynamic `RefId` entries, which are computed when OpenKNXproducer compiles them. Additionally, it means the translations live in a completely different place to the primary language text, which makes it hard to validate translations.

OpenKNXproducer can consume inline translations in the share XML without requiring you to manually edit the resulting `TranslationElement` nodes. Add `op:Translation` children to the element whose attributes you want to translate, and it will create the supporting `Languages`/`TranslationUnit`/`TranslationElement` entries that ETS expects.

## Namespace declaration

The OpenKNXproducer namespace must be declared on the root element of your XML file (it is typically already present):

```xml
xmlns:op="http://github.com/OpenKNX/OpenKNXproducer"
```

## Basic syntax

For each element you want to translate, add an `op:Translation` child that specifies the language and the attributes to translate. Use the original attribute name:

```xml
<ParameterSeparator Id="%AID%_PS-nnn" Text="Allgemein" UIHint="Headline">
  <op:Translation Language="en-US" Text="General" />
</ParameterSeparator>
```

If the element has multiple attributes to translate (you only need to translate what is visible in ETS), specify each of them in a single `op:Translation` element:

```xml
<ComObject Id="%AID%_O-%TT%00007" Text="Diagnose" FunctionText="Diagnoseobjekt">
  <op:Translation Language="en-US" Text="Diagnostics" FunctionText="Diagnostic object" />
</ComObject>
```

Translatable attribute names: `Text`, `SuffixText`, `FunctionText`.

## Rules

- `Language` is required on every `op:Translation` (e.g. `en-US`).
- At least one translatable attribute must be provided.
- The parent element **must** have a stable `Id` attribute — the generated `TranslationElement/@RefId` is taken from it.
- Do **not** add `op:Translation` to elements that have no `Id`.

## How it works

After processing, OpenKNXproducer:

1. Ensures the target language exists under both:
   - `/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram/Static/Languages`
   - `/KNX/ManufacturerData/Manufacturer/Languages`
2. Ensures a `TranslationUnit` exists for the current application `Id`.
3. For each `op:Translation`, adds a `TranslationElement` keyed by the parent element's `Id` and inserts a `Translation` child for each translated attribute.
4. Removes the inline `op:Translation` node.

No manual editing of the generated XML is necessary. When imported into ETS, it will show labels in the user's preferred language.

## Resulting ETS structure (example)

```xml
<Languages>
  <Language Identifier="en-US">
    <TranslationUnit RefId="M-00FA_A-AC11-04-0000">
      <TranslationElement RefId="M-00FA_A-AC11-04-0000_PT-OnOffYesNo_EN-0">
        <Translation AttributeName="Text" Text="No" />
      </TranslationElement>
    </TranslationUnit>
  </Language>
</Languages>
```

## Known limitation: renumbered IDs

OpenKNXproducer renumbers `_PB-` (ParameterBlock) and `_PS-` (ParameterSeparator) IDs during processing. Because the `TranslationElement/@RefId` is written from the parent element's `Id` before renumbering takes place, these entries will not match the final IDs that ETS looks up. Use explicit `TranslationElement` entries with stable IDs (e.g. derived from a `Name` attribute via XPath) as a workaround until this is resolved.