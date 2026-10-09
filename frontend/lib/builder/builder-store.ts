import type {
    BuilderChoices,
    CharacterBuilderState,
  } from "./types";
  
  const STORAGE_KEY =
    "lorebound-character-builders";
  
  function getAllBuilderStates():
    CharacterBuilderState[] {
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
    ) as CharacterBuilderState[];
  }
  
  function saveBuilderStates(
    states: CharacterBuilderState[]
  ) {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(states)
    );
  }
  
  export function getBuilderState(
    characterId: string
  ): CharacterBuilderState {
    const states = getAllBuilderStates();
  
    const existing = states.find(
      (state) =>
        state.characterId === characterId
    );
  
    if (existing) {
      return existing;
    }
  
    return {
      characterId,
      currentStep: 1,
      choices: {},
      updatedAt: new Date().toISOString(),
    };
  }
  
  export function saveBuilderState(
    characterId: string,
    data: {
      currentStep: number;
      choices: BuilderChoices;
    }
  ): CharacterBuilderState {
    const states = getAllBuilderStates();
  
    const updated: CharacterBuilderState = {
      characterId,
      currentStep: data.currentStep,
      choices: data.choices,
      updatedAt: new Date().toISOString(),
    };
  
    const exists = states.some(
      (state) =>
        state.characterId === characterId
    );
  
    saveBuilderStates(
      exists
        ? states.map((state) =>
            state.characterId === characterId
              ? updated
              : state
          )
        : [...states, updated]
    );
  
    return updated;
  }
  
  export function deleteBuilderState(
    characterId: string
  ) {
    saveBuilderStates(
      getAllBuilderStates().filter(
        (state) =>
          state.characterId !== characterId
      )
    );
  }