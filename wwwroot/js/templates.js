const templates = [

  {
    id: 'plan',
    title: 'Curriculum and lesson planning',
    templates: [

      {
        id: 'plan-lesson',
        title: 'Plan a lesson',
        messages: [
          { text: 'What is the subject or course name?', hint: 'e.g. "geography"' },
          { text: 'What are the learning objectives?', hint: 'By the end of this lesson, students will be able to...' }
        ],
        prompt: 'Plan a one-hour lesson for students in a UK secondary school. Design rich tasks and varied activities to get students thinking deeply. Individual and pair work are preferred instead of group work. Use the following structure, with detailed bullet points under each section:\n\n"""\n*Detailed lesson plan:*\n(Describe the lesson activities. When resources are used, just refer to them by number, e.g. (Resource 1). Do not write out the contents of the resource here. This will be written out in the next section.)\n\n* Retrieval practice\n* Awe and wonder\n* I do(explanation and modelling)\n* We do(co- construction and whole class questioning)\n* You do(individual practice)\n\n*I will now create each of these resources:*\n\n* Write the full text of each resource, don\'t just summarise it. Don\'t tell me to research or create my own resources. Create everything in full, using detailed paragraphs or bullet points.\n"""\n\nSubject: [0]\nLearning objectives: By the end of this lesson, students will be able to [1]\n\n*Detailed lesson plan:*',
        temperature: 0.4,
        model: 'gpt-4'
      },

      {
        id: 'plan-model',
        title: 'Write a model answer',
        templates: [

          {
            id: 'plan-model-answer',
            title: 'Write a model answer',
            messages: [
              { text: 'What is the subject or course name?', hint: 'e.g. "GCSE French"' },
              { text: 'What is the question?' }
            ],
            prompt: 'Write a model answer to this [0] question:\n\n"""\n[1]\n"""\n\nWrite a model answer, for the level of students studying [0] in a UK secondary school. Write paragraphs of text, without bullet points.\n\nModel answer:',
            temperature: 0.3,
            model: 'gpt-4'
          },

          {
            id: 'plan-model-answer-with-ms',
            title: 'Model answer using a mark scheme',
            messages: [
              { text: 'What is the subject or course name?', hint: 'e.g. "GCSE French"' },
              { text: 'What is the question?' },
              { text: 'Please enter the mark scheme.', hint: 'Only include level descriptors for the highest level.' }
            ],
            prompt: 'Write a model answer to this [0] question:\n\n"""\n[1]\n"""\n\nMark scheme:\n\n"""\n[2]\n"""\n\nWrite a model answer to score full marks, for the level of students studying [0] in a UK secondary school. Write paragraphs of text, without bullet points.\n\nQuestion: [1]\n\nModel answer:',
            temperature: 0.3,
            model: 'gpt-4'
          },

          {
            id: 'plan-model-answer-text-based',
            title: 'Model answer based on reading a text',
            messages: [
              { text: 'What is the subject or course name?', hint: 'e.g. "GCSE English Language"' },
              { text: 'What is the question?' },
              { text: 'Please paste the text.' },
            ],
            prompt: 'Write a model answer to this [0] question:\n\n"""\n[1]\n"""\n\nThe question is based on this text:\n\n"""\n[2]\n"""\n\nWrite a model answer, based on the text, for the level of students studying [0] in a UK secondary school. Write paragraphs of text, without bullet points.\n\nQuestion: [1]\n\nModel answer:',
            temperature: 0.3,
            model: 'gpt-4'
          },

        ]
      },

      {
        id: 'plan-questions',
        title: 'Generate questions',
        templates: [

          {
            id: 'plan-questions-multiple-choice',
            title: 'Generate multiple choice questions',
            messages: [
              { text: 'What is the subject or course name?', hint: 'e.g. "geography"' },
              { text: 'What is the topic?' }
            ],
            prompt: 'Generate 10 [0] questions about [1]. These should be multiple-choice questions suitable for secondary school students. For each question, include one correct answer and three distractors which are incorrect and contain common misconceptions.\n\nOutput format:\n\n1. Question\n    a. Answer 1\n    b. Answer 2 [CORRECT]\n    c. Answer 3\n    d. Answer 4\n\nRandomise the positions of the correct answers.',
            temperature: 0.2,
            model: 'gpt-4'
          },

          {
            id: 'plan-questions-short-answer',
            title: 'Generate short answer questions',
            messages: [
              { text: 'What is the subject or course name?', hint: 'e.g. "GCSE English Literature"' },
              { text: 'What is the topic?' }
            ],
            prompt: 'Generate 10 [0] questions about [1]. These should be short-answer questions suitable for secondary school students. Include the answers.\n\nOutput format:\n\n1. Question\n    * Answer',
            temperature: 0.2,
            model: 'gpt-4'
          },

          {
            id: 'plan-questions-comprehension',
            title: 'Generate comprehension questions for a text',
            messages: [
              { text: 'Paste the text below.' }
            ],
            prompt: 'Read this text and write 10 comprehension questions for secondary school students, to test that they have understood the meaning. Include the answers.\n\nOutput format:\n\n1. Question\n    * Answer\n\nText:\n"""\n[0]\n"""\n\nComprehension questions:',
            temperature: 0.2,
            model: 'gpt-4'
          }

        ]
      },

      {
        id: 'plan-explanation',
        title: 'Script an explanation',
        messages: [
          { text: 'What is the subject or course name?', hint: 'e.g. "A-Level Psychology"' },
          { text: 'What is the topic?' }
        ],
        prompt: 'Script a model explanation about "[1]", for students studying [0] in a UK secondary school. Tailor the explanation to this age group. Do not include a greeting, introduction, or goodbye. Explain the topic as clearly as possible, using analogies, examples, or links to existing knowledge where appropriate.\n\nTopic:\n[1]\n\nTeacher\'s explanation:',
        temperature: 0.3,
        model: 'gpt-4'
      },

      {
        id: 'plan-vocab',
        title: 'List Tier 3 vocabulary for a topic',
        messages: [
          { text: 'What is the subject or course name?', hint: 'e.g. "GCSE Business"' },
          { text: 'What is the topic?' }
        ],
        prompt: 'List some common Tier 3 vocabulary words related to the topic of "[1]". These should be subject-specific words that are appropriate for secondary school students to learn when studying [0].\n\nOutput format:\n\n1. Word\n    * Definition: ...\n    * Usage in a sentence: ...',
        temperature: 0.2
      },

      {
        id: 'plan-generate-text',
        title: 'Generate text about a topic',
        messages: [
          { text: 'What is the topic?' }
        ],
        prompt: 'Write a long, interesting, and informative text about the topic of "[0]".',
        temperature: 0.5
      }

    ]
  },

  {
    id: 'feedback',
    title: 'Feedback',
    templates: [
      {
        id: 'feedback-student-work',
        title: 'Give feedback on a student\'s work',
        messages: [
          { text: 'What is the subject or course name?', hint: 'e.g. "A-Level Music"' },
          { text: 'What is the question the student was asked?' },
          { text: 'Please paste the student\'s response.' },
        ],
        prompt: 'Write detailed feedback on this response to the [0] question:\n"""\n[1]\n"""\n\nStudent response:\n"""\n[2]\n"""\n\nYou will give feedback in the format:\n"""\nFEEDBACK:\nStrengths:\n\n* Detailed bullet points\n\nAreas to develop:\n\n* Detailed bullet points\n"""\n\nFEEDBACK:',
        temperature: 0,
        model: 'gpt-4'
      },

      {
        id: 'feedback-student-work-with-ms',
        title: 'Give feedback using a mark scheme',
        messages: [
          { text: 'What is the subject or course name?', hint: 'e.g. "GCSE Religious Studies"' },
          { text: 'What is the question the student was asked?' },
          { text: 'Please enter the mark scheme, or give any pointers such as key points that should be included.' },
          { text: 'Please paste the student\'s response.' },
        ],
        prompt: 'You are going to mark a student\'s response to this [0] question:\n"""\n[1]\n"""\n\nMark scheme:\n"""\n[2]\n"""\n\nStudent response:\n"""\n[3]\n"""\n\nYou will give feedback in the format:\n"""\nFEEDBACK:\nStrengths:\n\n* Detailed bullet points\n\nAreas to develop:\n\n* Detailed bullet points\n\nMark: X/X\n"""\n\nFEEDBACK:',
        temperature: 0,
        model: 'gpt-4'
      },

      {
        id: 'feedback-spreadsheet',
        title: 'Mark work for a whole class',
        feedbackMode: true,
        messages: [
          { text: 'Where multiple students have answered an essay question, I can help you mark their work and write individual feedback.\n\nPlease note:\n\n* This quickly uses up credits, so be mindful of setting up your spreadsheet correctly and providing a clear, AI-friendly mark scheme first time.\n* Do not ask me to mark work on a sensitive topic (such as literature or history involving violence or suffering, or controversial religious or ethical themes). Topics like these are very likely to trigger the content filter and the marking will fail.\n* You are responsible for checking my feedback before sharing it with students.\n\nTo get started, go to [office.com](https://www.office.com) and set up an Excel spreadsheet in your OneDrive with the following columns. The first two data rows contain the question and mark scheme, and subsequent rows contain student responses. The easiest way to collect responses is by setting a single long-answer question on Google Forms or Microsoft Forms.\n\n| Name | Response | Mark | Evaluation | Feedback | T Task | SPaG |\n| :-- | :-- | :-- | :-- | :-- | :-- | :-- |\n| Question | *(Type the question)* | | | | | |\n| Mark scheme | *(AI-friendly mark scheme)* | | | | | |\n| (Student) | (Their answer) | | | | |\n| ... | ... | | | | | |\n\nClick "Share" > "Link to this Sheet" and paste the link below.' }
        ],
        model: 'gpt-4'
      }

    ]
  },

  {
    id: 'comms',
    title: 'Communication',
    templates: [

      {
        id: 'comms-email-new',
        title: 'Compose an email',
        messages: [
          { text: 'Who is the recipient?', hint: 'e.g. "the headteacher"' },
          { text: 'What would you like to communicate in this letter? Include all key points.' },
        ],
        prompt: 'Write an email from a member of staff to [0]. Communicate the following key points, in a professional, positive style and appropriate tone:\n[1]\n\nEmail:',
        temperature: 0.4
      },

      {
        id: 'comms-email-reply',
        title: 'Reply to an email',
        messages: [
          { text: 'Who is the email from?', hint: 'e.g. "a parent"' },
          { text: 'Please paste the email you have received. Remember to remove any personal information.' },
          { text: 'How would you like to respond?' }
        ],
        prompt: 'I have received this email, as a member of staff. Please write a response, using a professional tone.\n\n' +
          'From: [0]\n\nMessage:\n\n"""\n[1]\n"""\n\nPlease respond to say:\n[2]\n\nResponse email:',
        temperature: 0.4
      },

      {
        id: 'comms-letter',
        title: 'Write a letter',
        messages: [
          { text: 'Who is the audience?', hint: 'e.g. "Year 11 parents"' },
          { text: 'What would you like to communicate in this letter? Include all key points.' },
        ],
        prompt: 'Write a detailed letter from a member of staff to [0]. Communicate the following key points, in a professional style and appropriate tone:\n[1]\n\nLetter:',
        temperature: 0.4
      },

      {
        id: 'comms-reference',
        title: 'Write a student reference',
        messages: [
          { text: 'What is the student\'s first name?' },
          { text: 'Summarise some of the student\'s qualities.' }
        ],
        prompt: 'Write a reference for our student, [0].\n\nStudent qualities:\n[1]\n\nReference from teacher:',
        temperature: 0.2,
        model: 'gpt-4'
      }

    ]
  },

  {
    id: 'hr',
    title: 'HR',
    templates: [

      {
        id: 'hr-job-advert',
        title: 'Write an advert, job description, and person specification',
        messages: [
          { text: 'What is the job title?', hint: 'e.g. "IT technician"' }
        ],
        prompt: 'Create a job advert (paragraphs), job description (bullet points), and person specification (bullet points) for the role of [0] in a UK secondary school. Ensure the information is clear and well-organised, using subheadings.',
        temperature: 0.3
      },

      {
        id: 'hr-interview-questions',
        title: 'Suggest interview questions',
        messages: [
          { text: 'What is the job title?', hint: 'e.g. "science teacher"' }
        ],
        prompt: 'Generate 10 interview questions for the role of [0] in a UK secondary school. Make sure the questions are open-ended and relevant to the role.\n\nUse the format:\n1. Question\n    * A good response might include...',
        temperature: 0.4
      },

      {
        id: 'hr-review-letter',
        title: 'Review a letter of application',
        messages: [
          { text: 'What is the job title?', hint: 'e.g. "English teacher"' },
          { text: 'Please paste the letter of application.' },
          { text: 'Please paste the person specification.' }
        ],
        prompt: '# Job title:\n\n[0]\n\n# Application letter:\n\n[1]\n\n# Person specification:\n\n[2]\n\n# Task:\n\nCreate a table with three columns:\n\n| Criterion | Evidence | Explanation |\n| :-- | :-- | :-- |\n\nFor each point in the person specification, indicate whether this is evidenced in the covering letter (Strong, Limited, or None). Explain your reasoning.',
        temperature: 0,
        model: 'gpt-4'
      }

    ]
  },

  {
    id: 'open',
    title: 'Other',
    messages: [
      { text: 'How can I help?' }
    ],
    prompt: '[0]',
    temperature: 0.4
  },

  {
    id: 'admin',
    title: 'Admin',
    messages: [
      { text: 'Enter admin command:' }
    ],
    admin: true
  }

];