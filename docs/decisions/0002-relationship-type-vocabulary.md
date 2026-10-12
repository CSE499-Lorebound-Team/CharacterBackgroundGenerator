# ADR 0002: Relationship types are suggested labels; narrowing ignores the type

- **Status:** Accepted
- **Date:** 2026-10-09
- **Issue:** P5-05 (#98)
- **Unblocks:** P7-02 (relationship-narrowed builder options)

## Context

A relationship links two entries of a setting with a `RelationshipType` (for
example Sharn *Capital of* Breland). The builder (P7-02) narrows a step's
options using these links: after a player picks a homeland, the culture step
should offer the cultures linked to it.

`RelationshipType` was free text, compared exactly. Narrowing only works if
the links it relies on can be found reliably, and free text drifts: "Ally of",
"ally of" and "Allied with" would all be different types.

## Questions and options

1. **Vocabulary.** Free text, a fixed catalog, or a suggested list plus free
   text? If there is a list, are types stored as readable labels
   (`Located in`) or as keys (`located_in`) that the frontend turns into
   labels?
2. **Which types drive narrowing?** Every type, or only a set such as
   `Native to`, `Located in`, `Practiced in`, `Member of`?
3. **Direction.** Does narrowing follow a link in either direction, or only
   from source to target?

A fixed catalog was rejected: GMs need to describe relationships the list
does not foresee ("Sworn rival of"). Keys were rejected: they add a key to
label mapping to the frontend, and custom types would display as
`sworn_rival_of`.

## Decision

1. **A suggested list plus free text, stored as readable labels and compared
   ignoring case.**
   - `RelationshipTypes.Suggested` (`api/Models/RelationshipTypes.cs`) is
     served by `GET /api/relationship-types` for the frontend to offer.
     Any other text, 1-60 characters, is accepted too.
   - On create and update, `RelationshipTypes.Normalize` trims the type,
     collapses inner whitespace to single spaces and, when the result matches
     a suggested type ignoring case, uses the suggested spelling (`located
     IN` becomes `Located in`). Custom types keep the case the GM typed.
   - The column is `citext`, so the unique (source, target, type) index, the
     duplicate check (409) and the list's `type` filter all ignore case. A
     check constraint keeps the 60-character limit, since `citext` has no
     length.
2. **Every relationship narrows, whatever its type.** The type is a label for
   people; the builder only asks "is this candidate linked to an earlier
   choice?". GMs do not have to pick special types for narrowing to work.
3. **Either direction.** A culture is narrowed in whether the GM wrote
   *Culture Native to Homeland* or *Homeland Home of Culture*.

## Rules for P7-02

- Candidates are the entries of the step's type in the character's setting,
  visible to the caller (`VisibleTo(role)`; a Player never gets GM-only
  entries).
- A candidate is linked when **any** relationship has the candidate at one
  end and an entry chosen in an earlier step at the other.
- Relationships with a GM-only end are ignored for Players
  (`VisibleTo(role)` for relationships), so narrowing never hints at hidden
  lore.
- At least one linked candidate: return only the linked ones,
  `narrowed: true`. None: return all candidates, `narrowed: false`.

## Consequences

- Migration `RelationshipTypeCaseInsensitive` turns `RelationshipType` into
  `citext` with a length check. It fails if a database already holds two
  links with the same source and target whose types differ only by case.
  Delete one first.
- Types already stored keep their spelling. `Normalize` applies only to new
  writes, so a type stored as "located in" before this change is not
  respelled until it is next updated.
- The suggested list can change freely: it only affects what the frontend
  offers and how new input is spelled, not stored data or narrowing.
- If a future feature needs type-specific behavior (for example only
  `Member of` grants faction perks), it should match on the normalized label
  ignoring case, or this ADR should be revisited for keys.
