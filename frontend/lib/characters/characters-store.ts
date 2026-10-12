import type {
    Character,
    CharacterStatus,
  } from "./types";
  
  const STORAGE_KEY =
    "lorebound-characters";
  
  function getAllCharacters(): Character[] {
    if (typeof window === "undefined") {
      return [];
    }
  
    const stored =
      window.localStorage.getItem(
        STORAGE_KEY
      );
  
    if (!stored) {
      return [];
    }
  
    return JSON.parse(
      stored
    ) as Character[];
  }
  
  function saveCharacters(
    characters: Character[]
  ) {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(characters)
    );
  }
  
  export function getCharacters(): Character[] {
    return getAllCharacters();
  }
  
  export function getCharacter(
    id: string
  ): Character | undefined {
    return getAllCharacters().find(
      (character) =>
        character.id === id
    );
  }
  
  export function createCharacter(
    name: string,
    settingId: string,
    settingName: string
  ): Character {
    const characters =
      getAllCharacters();
  
    const now =
      new Date().toISOString();
  
    const character: Character = {
      id: crypto.randomUUID(),
  
      name,
      settingId,
      settingName,
  
      status: "Draft",
      currentStep: 1,
  
      backstory: "",
  
      createdAt: now,
      updatedAt: now,
    };
  
    saveCharacters([
      ...characters,
      character,
    ]);
  
    return character;
  }
  
  export function updateCharacter(
    id: string,
    data: {
      name: string;
      settingId: string;
      settingName: string;
      status: CharacterStatus;
      backstory: string;
    }
  ): Character | undefined {
    const characters =
      getAllCharacters();
  
    const existing =
      characters.find(
        (character) =>
          character.id === id
      );
  
    if (!existing) {
      return undefined;
    }
  
    const updated: Character = {
      ...existing,
      ...data,
      updatedAt:
        new Date().toISOString(),
    };
  
    saveCharacters(
      characters.map((character) =>
        character.id === id
          ? updated
          : character
      )
    );
  
    return updated;
  }
  
  export function deleteCharacter(
    id: string
  ): boolean {
    const characters =
      getAllCharacters();
  
    const exists =
      characters.some(
        (character) =>
          character.id === id
      );
  
    if (!exists) {
      return false;
    }
  
    saveCharacters(
      characters.filter(
        (character) =>
          character.id !== id
      )
    );
  
    return true;
  }
  export function updateCharacterProgress(
    id: string,
    currentStep: number
  ): Character | undefined {
    const characters =
      getAllCharacters();
  
    const existing =
      characters.find(
        (character) =>
          character.id === id
      );
  
    if (!existing) {
      return undefined;
    }
  
    const updated: Character = {
      ...existing,
      currentStep,
      updatedAt:
        new Date().toISOString(),
    };
  
    saveCharacters(
      characters.map((character) =>
        character.id === id
          ? updated
          : character
      )
    );
  
    return updated;
  }