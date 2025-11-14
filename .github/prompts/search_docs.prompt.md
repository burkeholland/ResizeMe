---
agent: agent
---

<documentation_research_guidlines>
- Use the #context7 tool to search the official documentation for relevant information about the issue. If #context7 is not available, does not return useful information or the documentation is not available, do an <internet_search> for official documentation or reputable sources. Read these results and recursively follow any links that you find until you have found and read all of the appropriate documentation.
- Do an <internet_search> use the specific error message or issue description as the search query. Recursively follow any links that you find useful until you feel you have a complete understanding of the problem.
- Return to the chat, summarize your findings and propose the exact solution to the user.
</documentation_research_guidlines>

<internet_search>
- Use the #fetch tool to perform a web search for the specific error message or issue description using the query `google.com/search?q=your+search+query`.
- Ignore AI descriptions and summaries in the search results as they will not have accurate or reliable information.
- Follow any links from the search results that you find useful. If you do not find any useful information, refine your search query and try again.
- Recursively explore links until you have found a solution or have exhausted all reasonable options.
</internet_search>
